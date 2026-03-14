using System.Collections.Concurrent;
using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// 会话管理实现 —— PIN 生成/验证/访客追踪
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, GuestSession> _guests = new();

    public bool IsSessionActive { get; private set; }

    public string? CurrentPin { get; private set; }

    public int GuestCount => _guests.Count;

    public string StartSession()
    {
        // TODO: Phase 5 实现会话启动
        throw new NotImplementedException();
    }

    public bool ValidatePin(string pin)
    {
        // TODO: Phase 5 实现 PIN 验证
        throw new NotImplementedException();
    }

    public GuestSession RegisterGuest(string identifier)
    {
        // TODO: Phase 5 实现访客注册
        throw new NotImplementedException();
    }

    public bool ValidateGuestToken(string token)
    {
        // TODO: Phase 5 实现令牌验证
        throw new NotImplementedException();
    }

    public void EndSession()
    {
        // TODO: Phase 5 实现会话结束
        IsSessionActive = false;
        CurrentPin = null;
        _guests.Clear();
    }
}
