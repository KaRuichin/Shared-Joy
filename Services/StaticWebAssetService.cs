namespace Shared_Joy.Services;

/// <summary>
/// Web 静态资源解包服务实现
/// —— 将 Resources/Raw/WebClient 下的 MauiAsset 解包到可访问目录
/// GenHTTP 需要从文件系统目录提供静态文件，MauiAsset 在运行时为流式访问，
/// 因此需要先解包到 AppDataDirectory 下的临时目录。
/// </summary>
public class StaticWebAssetService : IStaticWebAssetService
{
    // 需要解包的 WebClient 资源文件列表
    private static readonly string[] AssetFiles = ["index.html", "style.css", "app.js"];

    public string WebRootPath { get; private set; } = string.Empty;

    public async Task ExtractAssetsAsync()
    {
        var targetDir = Path.Combine(FileSystem.AppDataDirectory, "WebClient");

        // 确保目标目录存在
        Directory.CreateDirectory(targetDir);

        foreach (var fileName in AssetFiles)
        {
            try
            {
                // MauiAsset 的路径格式：WebClient/{fileName}
                using var stream = await FileSystem.OpenAppPackageFileAsync($"WebClient/{fileName}");
                var targetPath = Path.Combine(targetDir, fileName);

                using var fileStream = File.Create(targetPath);
                await stream.CopyToAsync(fileStream);

                System.Diagnostics.Debug.WriteLine($"[WebAsset] 已解包: {fileName} → {targetPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAsset] 解包失败: {fileName} — {ex.Message}");
            }
        }

        WebRootPath = targetDir;
        System.Diagnostics.Debug.WriteLine($"[WebAsset] 资源解包完成, WebRoot={WebRootPath}");
    }
}
