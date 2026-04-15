using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// 会话管理接口 —— PIN 生成/验证/访客追踪
/// </summary>
public interface ISessionManager
{
    /// <summary>当前会话是否已启动</summary>
    bool IsSessionActive { get; }

    /// <summary>当前会话 PIN 码</summary>
    string? CurrentPin { get; }

    /// <summary>在线访客数量</summary>
    int GuestCount { get; }

    /// <summary>当前会话 ID（用于数据库记录关联）</summary>
    string? SessionId { get; }

    /// <summary>启动新会话，生成 PIN 码</summary>
    string StartSession();

    /// <summary>验证 PIN 码是否正确</summary>
    bool ValidatePin(string pin);

    /// <summary>注册访客并返回会话信息</summary>
    GuestSession RegisterGuest(string identifier);

    /// <summary>验证访客会话令牌</summary>
    bool ValidateGuestToken(string token);

    /// <summary>结束当前会话，清理所有访客和投票数据</summary>
    void EndSession();
}
