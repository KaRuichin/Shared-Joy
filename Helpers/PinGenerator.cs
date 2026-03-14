using System.Security.Cryptography;

namespace Shared_Joy.Helpers;

/// <summary>
/// PIN 码生成工具 —— 生成加密安全的 6 位数字 PIN
/// </summary>
public static class PinGenerator
{
    /// <summary>
    /// 生成 6 位随机数字 PIN 码（使用加密安全随机数）
    /// </summary>
    public static string Generate()
    {
        // 生成 0-999999 范围内的加密安全随机数
        var pin = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return pin.ToString("D6");
    }
}
