using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Shared_Joy.Helpers;

/// <summary>
/// 网络工具 —— 获取设备局域网 IP 地址
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// 获取设备当前局域网 IP 地址
    /// 优先返回 WiFi/以太网接口的 IPv4 地址
    /// </summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            // 遍历所有网络接口，优先查找已连接的 WiFi 或以太网
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211
                        or NetworkInterfaceType.Ethernet)
                .ToList();

            foreach (var networkInterface in interfaces)
            {
                var ipProperties = networkInterface.GetIPProperties();
                var address = ipProperties.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(a.Address));

                if (address is not null)
                    return address.Address.ToString();
            }

            // 后备方案：通过 DNS 获取
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var fallback = host.AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

            return fallback?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
