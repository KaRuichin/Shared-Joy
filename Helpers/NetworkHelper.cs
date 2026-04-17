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
    /// 获取设备当前局域网 IPv4 地址。
    ///
    /// 策略（按优先级）：
    /// 1. UDP socket 路由探测 —— 让系统选出口网卡，读本地端点 IP（最可靠，Android/Windows 均适用）
    /// 2. 网络接口遍历 —— Android 的 WiFi 接口类型为 Unknown，不能只过滤 Wireless80211/Ethernet
    /// 3. DNS 回退
    /// </summary>
    public static string GetLocalIpAddress()
    {
        // ── 方案 1：UDP 路由探测（不实际发包）──
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // 连接公共 DNS 地址仅用于触发路由选择，不会发送任何数据
            socket.Connect("8.8.8.8", 80);
            var localEndPoint = (IPEndPoint)socket.LocalEndPoint!;
            var ip = localEndPoint.Address.ToString();
            if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
            {
                System.Diagnostics.Debug.WriteLine($"[NetworkHelper] UDP 探测 IP: {ip}");
                return ip;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkHelper] UDP 探测失败: {ex.Message}");
        }

        // ── 方案 2：遍历网络接口（含 Unknown 类型，涵盖 Android WiFi）──
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(a.Address)
                    && !IsLinkLocal(a.Address))
                .ToList();

            // 优先选 WiFi/以太网，其次选其他类型
            var preferred = candidates.FirstOrDefault(a =>
            {
                var ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.GetIPProperties().UnicastAddresses.Contains(a));
                return ni?.NetworkInterfaceType is NetworkInterfaceType.Wireless80211
                    or NetworkInterfaceType.Ethernet;
            });

            var result = (preferred ?? candidates.FirstOrDefault())?.Address.ToString();
            if (!string.IsNullOrEmpty(result))
            {
                System.Diagnostics.Debug.WriteLine($"[NetworkHelper] 接口遍历 IP: {result}");
                return result;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkHelper] 接口遍历失败: {ex.Message}");
        }

        // ── 方案 3：DNS 回退 ──
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var dns = host.AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(a));
            if (dns is not null)
            {
                System.Diagnostics.Debug.WriteLine($"[NetworkHelper] DNS 回退 IP: {dns}");
                return dns.ToString();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkHelper] DNS 回退失败: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine("[NetworkHelper] 所有方案失败，返回 127.0.0.1");
        return "127.0.0.1";
    }

    /// <summary>判断是否为链路本地地址（169.254.x.x），此类地址不可路由</summary>
    private static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }
}
