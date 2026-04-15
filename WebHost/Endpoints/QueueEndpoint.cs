using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Webservices;
using Shared_Joy.Services;

namespace Shared_Joy.WebHost.Endpoints;

/// <summary>
/// 队列查看 API 端点
/// GET /api/queue — 返回当前投票排行队列
/// </summary>
public class QueueEndpoint
{
    private readonly IVotingEngine _votingEngine;

    public QueueEndpoint(IVotingEngine votingEngine)
    {
        _votingEngine = votingEngine;
    }

    /// <summary>
    /// GET /api/queue
    /// 返回按票数排序的投票队列
    /// </summary>
    [ResourceMethod]
    public QueueResponse GetQueue()
    {
        var ranked = _votingEngine.GetRankedQueue();

        return new QueueResponse
        {
            Items = ranked.Select(v => new QueueItemDto
            {
                Track = new TrackDto
                {
                    Id = v.Track.Id,
                    Name = v.Track.Name,
                    Artists = v.Track.Artists,
                    AlbumName = v.Track.AlbumName,
                    AlbumImageUrl = v.Track.AlbumImageUrl,
                    Uri = v.Track.Uri,
                    DurationMs = v.Track.DurationMs
                },
                VoteCount = v.VoteCount,
                VoterIds = v.VoterIds.ToList()
            }).ToList()
        };
    }
}

public class QueueResponse
{
    public List<QueueItemDto> Items { get; set; } = [];
}

public class QueueItemDto
{
    public TrackDto Track { get; set; } = new();
    public int VoteCount { get; set; }
    public List<string> VoterIds { get; set; } = [];
}
