using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Webservices;
using Shared_Joy.Models;
using Shared_Joy.Services;

namespace Shared_Joy.WebHost.Endpoints;

/// <summary>
/// 投票/取消投票 API 端点
/// POST /api/vote — 投票（请求体含歌曲信息 + guestId）
/// DELETE /api/vote?trackId={trackId}&amp;guestId={guestId} — 取消投票
/// </summary>
public class VoteEndpoint
{
    private readonly IVotingEngine _votingEngine;

    public VoteEndpoint(IVotingEngine votingEngine)
    {
        _votingEngine = votingEngine;
    }

    /// <summary>
    /// POST /api/vote
    /// 请求体: { "guestId": "...", "track": { "id": "...", "name": "...", ... } }
    /// </summary>
    [ResourceMethod(RequestMethod.Post)]
    public VoteResponse PostVote(VoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GuestId) || request.Track is null ||
            string.IsNullOrWhiteSpace(request.Track.Id))
        {
            return new VoteResponse { Success = false, Error = "Invalid vote request" };
        }

        // 简单校验 trackId 格式（Spotify track ID 为 22 位字母数字）
        if (request.Track.Id.Length > 30)
        {
            return new VoteResponse { Success = false, Error = "Invalid track ID format" };
        }

        var track = new SpotifyTrack
        {
            Id = request.Track.Id,
            Name = request.Track.Name,
            Artists = request.Track.Artists,
            AlbumName = request.Track.AlbumName,
            AlbumImageUrl = request.Track.AlbumImageUrl,
            Uri = request.Track.Uri,
            DurationMs = request.Track.DurationMs
        };

        var success = _votingEngine.Vote(request.GuestId, track);

        return new VoteResponse
        {
            Success = success,
            Error = success ? null : "Already voted for this track"
        };
    }

    /// <summary>
    /// DELETE /api/vote?trackId=...&amp;guestId=...
    /// </summary>
    [ResourceMethod(RequestMethod.Delete)]
    public VoteResponse DeleteVote(string trackId, string guestId)
    {
        if (string.IsNullOrWhiteSpace(trackId) || string.IsNullOrWhiteSpace(guestId))
        {
            return new VoteResponse { Success = false, Error = "trackId and guestId are required" };
        }

        var success = _votingEngine.Unvote(guestId, trackId);

        return new VoteResponse
        {
            Success = success,
            Error = success ? null : "Vote not found"
        };
    }
}

public class VoteRequest
{
    public string GuestId { get; set; } = string.Empty;
    public TrackDto? Track { get; set; }
}

public class VoteResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
