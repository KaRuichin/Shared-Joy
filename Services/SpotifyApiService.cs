using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// Spotify Web API 交互服务实现
/// 
/// 所有请求自动附加 Bearer Token，遇 401 时自动刷新 Token 并重试一次。
/// 使用 System.Text.Json 手动解析 Spotify 响应，仅提取所需字段。
/// </summary>
public class SpotifyApiService : ISpotifyApiService
{
    private const string BaseUrl = "https://api.spotify.com/v1";

    private readonly ISpotifyAuthService _authService;
    private readonly HttpClient _httpClient;

    public SpotifyApiService(ISpotifyAuthService authService)
    {
        _authService = authService;
        _httpClient = new HttpClient();
    }

    #region 公开 API 方法

    /// <summary>
    /// 搜索歌曲 — GET /v1/search?type=track
    /// </summary>
    public async Task<List<SpotifyTrack>> SearchTracksAsync(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var url = $"{BaseUrl}/search?type=track&q={Uri.EscapeDataString(query)}&limit={limit}";

        try
        {
            var response = await SendWithRetryAsync(HttpMethod.Get, url);
            if (response is null || !response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (!json.TryGetProperty("tracks", out var tracksObj) ||
                !tracksObj.TryGetProperty("items", out var items))
                return [];

            var results = new List<SpotifyTrack>();
            foreach (var item in items.EnumerateArray())
            {
                var track = ParseTrack(item);
                if (track is not null)
                    results.Add(track);
            }

            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 搜索 '{query}' 返回 {results.Count} 首歌曲");
            return results;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 搜索异常: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// 获取当前播放状态 — GET /v1/me/player
    /// 无活跃设备时返回 null（204 No Content）
    /// </summary>
    public async Task<PlaybackState?> GetCurrentPlaybackAsync()
    {
        try
        {
            var response = await SendWithRetryAsync(HttpMethod.Get, $"{BaseUrl}/me/player");
            if (response is null)
                return null;

            // 204 No Content = 无活跃设备
            if (response.StatusCode == HttpStatusCode.NoContent)
                return null;

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            var state = new PlaybackState
            {
                IsPlaying = json.TryGetProperty("is_playing", out var isPlaying) && isPlaying.GetBoolean(),
                ProgressMs = json.TryGetProperty("progress_ms", out var progress) ? progress.GetInt32() : 0,
            };

            // 解析设备名称
            if (json.TryGetProperty("device", out var device) &&
                device.TryGetProperty("name", out var deviceName))
            {
                state.DeviceName = deviceName.GetString() ?? string.Empty;
            }

            // 解析当前播放歌曲
            if (json.TryGetProperty("item", out var item) &&
                item.ValueKind == JsonValueKind.Object)
            {
                state.CurrentTrack = ParseTrack(item);
            }

            return state;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 获取播放状态异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将歌曲添加到播放队列 — POST /v1/me/player/queue?uri={trackUri}
    /// </summary>
    public async Task<bool> AddToQueueAsync(string trackUri)
    {
        if (string.IsNullOrWhiteSpace(trackUri))
            return false;

        try
        {
            var url = $"{BaseUrl}/me/player/queue?uri={Uri.EscapeDataString(trackUri)}";
            var response = await SendWithRetryAsync(HttpMethod.Post, url);

            var success = response?.StatusCode == HttpStatusCode.NoContent;
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 添加到队列 {trackUri}: {(success ? "成功" : "失败")}");
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 添加到队列异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 开始播放 — PUT /v1/me/player/play
    /// </summary>
    public async Task<bool> PlayAsync()
    {
        try
        {
            var response = await SendWithRetryAsync(HttpMethod.Put, $"{BaseUrl}/me/player/play");
            var success = response?.IsSuccessStatusCode == true;
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 播放: {(success ? "成功" : "失败")}");
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 播放异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 暂停播放 — PUT /v1/me/player/pause
    /// </summary>
    public async Task<bool> PauseAsync()
    {
        try
        {
            var response = await SendWithRetryAsync(HttpMethod.Put, $"{BaseUrl}/me/player/pause");
            var success = response?.IsSuccessStatusCode == true;
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 暂停: {(success ? "成功" : "失败")}");
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 暂停异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 跳到下一首 — POST /v1/me/player/next
    /// </summary>
    public async Task<bool> SkipNextAsync()
    {
        try
        {
            var response = await SendWithRetryAsync(HttpMethod.Post, $"{BaseUrl}/me/player/next");
            var success = response?.IsSuccessStatusCode == true;
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 跳过: {(success ? "成功" : "失败")}");
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 跳过异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取播放队列 — GET /v1/me/player/queue
    /// </summary>
    public async Task<List<SpotifyTrack>> GetQueueAsync()
    {
        try
        {
            var response = await SendWithRetryAsync(HttpMethod.Get, $"{BaseUrl}/me/player/queue");
            if (response is null || !response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (!json.TryGetProperty("queue", out var queueArray))
                return [];

            var results = new List<SpotifyTrack>();
            foreach (var item in queueArray.EnumerateArray())
            {
                var track = ParseTrack(item);
                if (track is not null)
                    results.Add(track);
            }

            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 队列中有 {results.Count} 首歌曲");
            return results;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 获取队列异常: {ex.Message}");
            return [];
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 发送带 Bearer Token 的请求，遇 401 时自动刷新 Token 并重试一次
    /// </summary>
    private async Task<HttpResponseMessage?> SendWithRetryAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        // 第一次尝试
        var token = await _authService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            System.Diagnostics.Debug.WriteLine("[SpotifyApi] 无可用 Token，跳过请求");
            return null;
        }

        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        // 401 → 刷新 Token 后重试一次
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            System.Diagnostics.Debug.WriteLine("[SpotifyApi] 收到 401，尝试刷新 Token 后重试");

            token = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return null;

            // 重建请求（HttpRequestMessage 不可重用）
            var retryRequest = new HttpRequestMessage(method, url) { Content = content };
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            response = await _httpClient.SendAsync(retryRequest);
        }

        return response;
    }

    /// <summary>
    /// 从 Spotify JSON 响应解析单首歌曲为 SpotifyTrack
    /// 被 SearchTracksAsync、GetCurrentPlaybackAsync、GetQueueAsync 共用
    /// </summary>
    private static SpotifyTrack? ParseTrack(JsonElement item)
    {
        try
        {
            // 跳过非 track 类型（如 episode）
            if (item.TryGetProperty("type", out var typeProp) &&
                typeProp.GetString() != "track")
                return null;

            var track = new SpotifyTrack
            {
                Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                Uri = item.TryGetProperty("uri", out var uri) ? uri.GetString() ?? string.Empty : string.Empty,
                DurationMs = item.TryGetProperty("duration_ms", out var duration) ? duration.GetInt32() : 0,
            };

            // 解析艺术家列表（逗号分隔）
            if (item.TryGetProperty("artists", out var artists) &&
                artists.ValueKind == JsonValueKind.Array)
            {
                var artistNames = new List<string>();
                foreach (var artist in artists.EnumerateArray())
                {
                    if (artist.TryGetProperty("name", out var artistName))
                        artistNames.Add(artistName.GetString() ?? string.Empty);
                }
                track.Artists = string.Join(", ", artistNames);
            }

            // 解析专辑信息
            if (item.TryGetProperty("album", out var album))
            {
                track.AlbumName = album.TryGetProperty("name", out var albumName)
                    ? albumName.GetString() ?? string.Empty : string.Empty;

                // 取第一张专辑封面图片
                if (album.TryGetProperty("images", out var images) &&
                    images.ValueKind == JsonValueKind.Array &&
                    images.GetArrayLength() > 0)
                {
                    var firstImage = images[0];
                    if (firstImage.TryGetProperty("url", out var imageUrl))
                        track.AlbumImageUrl = imageUrl.GetString() ?? string.Empty;
                }
            }

            return track;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpotifyApi] 解析歌曲异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从 HttpResponseMessage 读取 JSON（辅助扩展）
    /// </summary>
    private static async Task<JsonElement> ReadFromJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<JsonElement>(stream);
    }

    #endregion
}
