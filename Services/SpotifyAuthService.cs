using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared_Joy.Services;

/// <summary>
/// Spotify OAuth 2.0 PKCE 认证服务实现
/// 
/// 认证流程：
/// 1. 生成 PKCE code_verifier / code_challenge
/// 2. 打开 Spotify 授权页面
/// 3. 用户授权后回调，获取 authorization_code
/// 4. 用 code + code_verifier 换取 access_token / refresh_token
/// 5. Token 存储到 SecureStorage，过期时自动刷新
/// </summary>
public class SpotifyAuthService : ISpotifyAuthService
{
    // Spotify OAuth 端点
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string UserProfileUrl = "https://api.spotify.com/v1/me";

    // 回调 URI（Android 使用 Deep Link，Windows 使用 loopback）
    private const string AndroidCallbackUri = "sharedjoy://callback";
    // Windows 使用 127.0.0.1 loopback 回调（端口在运行时动态分配）
    private const int WindowsLoopbackPort = 5432;
    private static readonly string WindowsCallbackUri = $"http://127.0.0.1:{WindowsLoopbackPort}/callback";

    // Spotify API 所需权限范围
    private const string Scopes = "user-read-playback-state user-modify-playback-state user-read-currently-playing streaming";

    // SecureStorage 键名
    private const string KeyAccessToken = "spotify_access_token";
    private const string KeyRefreshToken = "spotify_refresh_token";
    private const string KeyTokenExpiry = "spotify_token_expiry";

    // Preferences 键名
    private const string KeyClientId = "spotify_client_id";

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // 内存中缓存的令牌信息
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    // PKCE 临时变量（仅在认证流程中使用）
    private string? _codeVerifier;

    public bool IsAuthenticated => _accessToken is not null && _refreshToken is not null;

    public event EventHandler<bool>? AuthenticationChanged;

    public SpotifyAuthService()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// 启动 OAuth 2.0 PKCE 认证流程
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            var clientId = GetClientId();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                System.Diagnostics.Debug.WriteLine("[SpotifyAuth] Client ID 未配置");
                return false;
            }

            // 1. 生成 PKCE code_verifier 和 code_challenge
            _codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(_codeVerifier);

            // 2. 根据平台选择回调 URI
            var callbackUri = GetPlatformCallbackUri();

            // 3. 构造授权 URL
            var state = GenerateRandomString(16);
            var authUrl = BuildAuthorizeUrl(clientId, callbackUri, codeChallenge, state);

            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 启动认证，回调 URI: {callbackUri}");

            // 4. 根据平台执行不同的认证流程
            string? authorizationCode;

#if WINDOWS
            authorizationCode = await AuthenticateWindowsAsync(authUrl, state);
#else
            authorizationCode = await AuthenticateMobileAsync(authUrl, callbackUri);
#endif

            if (string.IsNullOrEmpty(authorizationCode))
            {
                System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 未获取到授权码");
                return false;
            }

            // 5. 用授权码换取令牌
            var success = await ExchangeCodeForTokenAsync(clientId, authorizationCode, callbackUri);

            if (success)
            {
                AuthenticationChanged?.Invoke(this, true);
                System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 认证成功");
            }

            return success;
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 用户取消了认证");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 认证异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取当前有效的访问令牌（自动刷新过期令牌）
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            if (_accessToken is null)
                return null;

            // 提前 60 秒刷新，避免边界情况
            if (DateTime.UtcNow.AddSeconds(60) >= _tokenExpiry)
            {
                System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 令牌即将过期，自动刷新");
                var refreshed = await RefreshAccessTokenAsync();
                if (!refreshed)
                {
                    System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 令牌刷新失败");
                    return null;
                }
            }

            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// 注销并清除所有令牌
    /// </summary>
    public async Task LogoutAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _tokenExpiry = DateTime.MinValue;

        // 清除 SecureStorage 和 Preferences 中的令牌
        SecureStorage.Remove(KeyAccessToken);
        SecureStorage.Remove(KeyRefreshToken);
        SecureStorage.Remove(KeyTokenExpiry);
        Preferences.Remove($"fallback_{KeyAccessToken}");
        Preferences.Remove($"fallback_{KeyRefreshToken}");
        Preferences.Remove($"fallback_{KeyTokenExpiry}");

        AuthenticationChanged?.Invoke(this, false);
        System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 已注销");

        await Task.CompletedTask;
    }

    /// <summary>
    /// 尝试从 SecureStorage 恢复令牌
    /// </summary>
    public async Task<bool> TryRestoreTokenAsync()
    {
        try
        {
            // 优先从 SecureStorage 读取
            var accessToken = await ReadSecureAsync(KeyAccessToken);
            var refreshToken = await ReadSecureAsync(KeyRefreshToken);
            var expiryStr = await ReadSecureAsync(KeyTokenExpiry);

            if (string.IsNullOrEmpty(refreshToken))
            {
                System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 无已保存的 refresh_token");
                return false;
            }

            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _tokenExpiry = DateTime.TryParse(expiryStr, out var expiry) ? expiry : DateTime.MinValue;

            // refresh_token 存在即可恢复——access_token 过期或为空都通过刷新解决
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiry)
            {
                System.Diagnostics.Debug.WriteLine("[SpotifyAuth] access_token 缺失或已过期，尝试刷新");
                var refreshed = await RefreshAccessTokenAsync();
                if (!refreshed)
                {
                    System.Diagnostics.Debug.WriteLine("[SpotifyAuth] refresh_token 刷新失败，需要重新授权");
                    await LogoutAsync();
                    return false;
                }
            }

            AuthenticationChanged?.Invoke(this, true);
            System.Diagnostics.Debug.WriteLine("[SpotifyAuth] 令牌恢复成功");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 令牌恢复异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 安全读取 SecureStorage，Android Keystore 损坏时自动清理并回退到 Preferences
    /// </summary>
    private static async Task<string?> ReadSecureAsync(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(key);
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        catch (Exception ex)
        {
            // Android: Keystore 密钥在 debug 重签名后可能失效
            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] SecureStorage 读取 '{key}' 失败: {ex.Message}");
            SecureStorage.Remove(key);
        }

        // 回退到 Preferences
        var fallback = Preferences.Get($"fallback_{key}", string.Empty);
        if (!string.IsNullOrEmpty(fallback))
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 从 Preferences 回退读取 '{key}' 成功");
            return fallback;
        }

        return null;
    }

    /// <summary>
    /// 双写 SecureStorage + Preferences（确保 Android 重启后可恢复）
    /// </summary>
    private static async Task WriteSecureAsync(string key, string value)
    {
        try
        {
            await SecureStorage.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] SecureStorage 写入 '{key}' 失败: {ex.Message}");
        }

        // 始终同步写入 Preferences 作为回退
        Preferences.Set($"fallback_{key}", value);
    }

    /// <summary>
    /// 获取当前用户显示名称
    /// </summary>
    public async Task<string?> GetUserDisplayNameAsync()
    {
        var profile = await GetUserProfileAsync();
        return profile?.DisplayName;
    }

    /// <summary>
    /// 获取当前用户头像 URL
    /// </summary>
    public async Task<string?> GetUserAvatarUrlAsync()
    {
        var profile = await GetUserProfileAsync();
        return profile?.AvatarUrl;
    }

    /// <summary>
    /// 获取用户 Profile 信息（内部缓存避免重复请求）
    /// </summary>
    private async Task<UserProfile?> GetUserProfileAsync()
    {
        var token = await GetAccessTokenAsync();
        if (token is null) return null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, UserProfileUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            var displayName = json.TryGetProperty("display_name", out var nameProp)
                ? nameProp.GetString() : null;

            // Spotify images 数组：取第一张（通常是最大尺寸）
            string? avatarUrl = null;
            if (json.TryGetProperty("images", out var imagesProp) &&
                imagesProp.ValueKind == JsonValueKind.Array &&
                imagesProp.GetArrayLength() > 0)
            {
                var firstImage = imagesProp[0];
                if (firstImage.TryGetProperty("url", out var urlProp))
                {
                    avatarUrl = urlProp.GetString();
                }
            }

            return new UserProfile(displayName, avatarUrl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 获取用户信息异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>用户 Profile 内部记录</summary>
    private sealed record UserProfile(string? DisplayName, string? AvatarUrl);

    #region 平台特定认证流程

    /// <summary>
    /// Android/iOS 认证：使用 WebAuthenticator
    /// </summary>
    private async Task<string?> AuthenticateMobileAsync(string authUrl, string callbackUri)
    {
        var result = await WebAuthenticator.Default.AuthenticateAsync(
            new Uri(authUrl),
            new Uri(callbackUri));

        // 从回调 URL 中提取授权码
        return result?.Properties.TryGetValue("code", out var code) == true ? code : null;
    }

    /// <summary>
    /// Windows 认证：启动系统浏览器 + 本地 loopback HTTP 监听回调
    /// </summary>
    private async Task<string?> AuthenticateWindowsAsync(string authUrl, string expectedState)
    {
        string? authorizationCode = null;
        var listener = new System.Net.HttpListener();
        var listenerPrefix = $"http://127.0.0.1:{WindowsLoopbackPort}/";

        try
        {
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();

            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] Windows loopback 监听已启动: {listenerPrefix}");

            // 打开系统默认浏览器
            await Browser.Default.OpenAsync(new Uri(authUrl), BrowserLaunchMode.SystemPreferred);

            // 等待回调（超时 120 秒）
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var context = await listener.GetContextAsync().WaitAsync(cts.Token);

            var query = context.Request.QueryString;
            var code = query["code"];
            var state = query["state"];
            var error = query["error"];

            // 返回成功/失败页面给浏览器
            var responseHtml = string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(code)
                ? "<html><body style='font-family:sans-serif;text-align:center;padding:40px'><h2>✅ Authorization successful!</h2><p>You can close this window and return to Shared Joy.</p></body></html>"
                : $"<html><body style='font-family:sans-serif;text-align:center;padding:40px'><h2>❌ Authorization failed</h2><p>{error ?? "Unknown error"}</p></body></html>";

            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.Close();

            // 验证 state 防止 CSRF
            if (state != expectedState)
            {
                System.Diagnostics.Debug.WriteLine("[SpotifyAuth] state 不匹配，可能存在 CSRF 攻击");
                return null;
            }

            if (!string.IsNullOrEmpty(error))
            {
                System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 授权错误: {error}");
                return null;
            }

            authorizationCode = code;
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }

        return authorizationCode;
    }

    #endregion

    #region Token 交换与刷新

    /// <summary>
    /// 用授权码换取 access_token 和 refresh_token
    /// </summary>
    private async Task<bool> ExchangeCodeForTokenAsync(string clientId, string code, string redirectUri)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = _codeVerifier!
        });

        return await RequestTokenAsync(content);
    }

    /// <summary>
    /// 使用 refresh_token 刷新 access_token
    /// </summary>
    private async Task<bool> RefreshAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken))
            return false;

        var clientId = GetClientId();
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _refreshToken,
            ["client_id"] = clientId
        });

        return await RequestTokenAsync(content);
    }

    /// <summary>
    /// 发送令牌请求并保存结果
    /// </summary>
    private async Task<bool> RequestTokenAsync(FormUrlEncodedContent content)
    {
        try
        {
            var response = await _httpClient.PostAsync(TokenUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 令牌请求失败 ({response.StatusCode}): {errorBody}");
                return false;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                return false;

            // 更新内存缓存
            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            // refresh_token 可能不会在每次刷新时返回，仅在有值时更新
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                _refreshToken = tokenResponse.RefreshToken;

            // 持久化到 SecureStorage + Preferences 双写（防止 Android Keystore 失效）
            var expiryStr = _tokenExpiry.ToString("O");
            await WriteSecureAsync(KeyAccessToken, _accessToken);
            await WriteSecureAsync(KeyRefreshToken, _refreshToken!);
            await WriteSecureAsync(KeyTokenExpiry, expiryStr);

            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 令牌已更新，过期时间: {_tokenExpiry:u}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyAuth] 令牌请求异常: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region PKCE 辅助方法

    /// <summary>
    /// 生成 PKCE code_verifier（43-128 字符的随机字符串）
    /// </summary>
    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// 从 code_verifier 生成 code_challenge（SHA256 哈希后 Base64URL 编码）
    /// </summary>
    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Base64URL 编码（RFC 7636）
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// 生成加密安全的随机字符串
    /// </summary>
    private static string GenerateRandomString(int length)
    {
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes)[..length];
    }

    #endregion

    #region URL 构建

    /// <summary>
    /// 构造 Spotify 授权 URL
    /// </summary>
    private static string BuildAuthorizeUrl(string clientId, string redirectUri, string codeChallenge, string state)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = codeChallenge,
            ["state"] = state,
            ["scope"] = Scopes
        };

        var queryString = string.Join("&", queryParams.Select(
            kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{AuthorizeUrl}?{queryString}";
    }

    /// <summary>
    /// 获取当前平台的回调 URI
    /// </summary>
    private static string GetPlatformCallbackUri()
    {
#if WINDOWS
        return WindowsCallbackUri;
#elif ANDROID
        return AndroidCallbackUri;
#else
        return AndroidCallbackUri;
#endif
    }

    /// <summary>
    /// 从 Preferences 获取 Client ID
    /// </summary>
    private static string? GetClientId()
    {
        return Preferences.Get(KeyClientId, string.Empty);
    }

    #endregion

    #region 内部模型

    /// <summary>
    /// Spotify Token 响应模型
    /// </summary>
    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }

    #endregion
}
