using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Webservices;
using Shared_Joy.Services;

namespace Shared_Joy.WebHost.Endpoints;

/// <summary>
/// Spotify 搜索代理 API 端点
/// GET /api/search?q={query} — 代理到 Spotify 搜索
/// </summary>
public class SearchEndpoint
{
    private readonly ISpotifyApiService _spotifyApi;

    public SearchEndpoint(ISpotifyApiService spotifyApi)
    {
        _spotifyApi = spotifyApi;
    }

    /// <summary>
    /// GET /api/search?q=...
    /// 返回 Spotify 搜索结果列表
    /// </summary>
    [ResourceMethod]
    public async Task<SearchResponse> GetSearch(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return new SearchResponse { Tracks = [] };
        }

        // 限制查询长度，防止滥用
        if (q.Length > 100)
            q = q[..100];

        var tracks = await _spotifyApi.SearchTracksAsync(q, 15);

        return new SearchResponse
        {
            Tracks = tracks.Select(t => new TrackDto
            {
                Id = t.Id,
                Name = t.Name,
                Artists = t.Artists,
                AlbumName = t.AlbumName,
                AlbumImageUrl = t.AlbumImageUrl,
                Uri = t.Uri,
                DurationMs = t.DurationMs
            }).ToList()
        };
    }
}

public class SearchResponse
{
    public List<TrackDto> Tracks { get; set; } = [];
}

/// <summary>
/// 歌曲 DTO —— 用于 REST API JSON 序列化
/// </summary>
public class TrackDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artists { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public string AlbumImageUrl { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int DurationMs { get; set; }
}
