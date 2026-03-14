namespace Shared_Joy.Models;

/// <summary>
/// 访客会话模型
/// </summary>
public class GuestSession
{
    /// <summary>访客唯一标识</summary>
    public string GuestId { get; set; } = string.Empty;

    /// <summary>会话令牌</summary>
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>会话创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActiveAt { get; set; }
}
