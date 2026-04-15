using System.Collections.Concurrent;
using System.Security.Cryptography;
using Shared_Joy.Helpers;
using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// 会话管理实现 —— PIN 生成/验证/访客追踪
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, GuestSession> _guests = new();

    // 令牌 → GuestId 的反向索引，加速 ValidateGuestToken 查找
    private readonly ConcurrentDictionary<string, string> _tokenIndex = new();

    private string? _sessionId;

    public bool IsSessionActive { get; private set; }

    public string? CurrentPin { get; private set; }

    public int GuestCount => _guests.Count;

    /// <summary>当前会话 ID（用于数据库记录关联）</summary>
    public string? SessionId => _sessionId;

    public string StartSession()
    {
        // 生成加密安全的 6 位 PIN
        CurrentPin = PinGenerator.Generate();
        _sessionId = Guid.NewGuid().ToString("N");
        IsSessionActive = true;
        _guests.Clear();
        _tokenIndex.Clear();

        System.Diagnostics.Debug.WriteLine($"[Session] 会话已启动, PIN={CurrentPin}, SessionId={_sessionId}");
        return CurrentPin;
    }

    public bool ValidatePin(string pin)
    {
        if (!IsSessionActive || CurrentPin is null)
            return false;

        // 常量时间比较，防止计时攻击
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(pin),
            System.Text.Encoding.UTF8.GetBytes(CurrentPin));
    }

    public GuestSession RegisterGuest(string identifier)
    {
        // 如果该标识符已注册，直接返回已有会话（幂等）
        if (_guests.TryGetValue(identifier, out var existing))
        {
            existing.LastActiveAt = DateTime.UtcNow;
            return existing;
        }

        // 生成加密安全的会话令牌（32 字节 = 256 位）
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var sessionToken = Convert.ToBase64String(tokenBytes);

        var guest = new GuestSession
        {
            GuestId = identifier,
            SessionToken = sessionToken,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };

        _guests[identifier] = guest;
        _tokenIndex[sessionToken] = identifier;

        System.Diagnostics.Debug.WriteLine($"[Session] 访客已注册: {identifier}, 当前在线: {GuestCount}");
        return guest;
    }

    public bool ValidateGuestToken(string token)
    {
        if (!IsSessionActive || string.IsNullOrEmpty(token))
            return false;

        // 通过令牌索引快速查找
        if (!_tokenIndex.TryGetValue(token, out var guestId))
            return false;

        // 更新最后活跃时间
        if (_guests.TryGetValue(guestId, out var guest))
        {
            guest.LastActiveAt = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    public void EndSession()
    {
        IsSessionActive = false;
        CurrentPin = null;
        _sessionId = null;
        _guests.Clear();
        _tokenIndex.Clear();

        System.Diagnostics.Debug.WriteLine("[Session] 会话已结束");
    }
}
