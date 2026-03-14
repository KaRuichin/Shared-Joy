namespace Shared_Joy.Services;

/// <summary>
/// Web 静态资源解包服务实现
/// —— 将 Resources/Raw/WebClient 下的 MauiAsset 解包到可访问目录
/// </summary>
public class StaticWebAssetService : IStaticWebAssetService
{
    public string WebRootPath { get; private set; } = string.Empty;

    public Task ExtractAssetsAsync()
    {
        // TODO: Phase 6 实现资源解包
        throw new NotImplementedException();
    }
}
