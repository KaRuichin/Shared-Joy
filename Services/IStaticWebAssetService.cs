namespace Shared_Joy.Services;

/// <summary>
/// Web 静态资源解包服务接口
/// —— 将 Resources/Raw/WebClient 下的 MauiAsset 解包到可访问目录
/// </summary>
public interface IStaticWebAssetService
{
    /// <summary>解包后的静态资源根目录路径</summary>
    string WebRootPath { get; }

    /// <summary>将嵌入资源解包到本地目录</summary>
    Task ExtractAssetsAsync();
}
