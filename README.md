[中文](README.md) | [English](README.en.md)

# 🎵 众乐 (Shared Joy)

将手机或电脑变成**本地 Spotify 点唱机**：主机运行 **.NET MAUI + XAML** 应用，访客通过同一局域网浏览器扫码进入页面，输入 PIN 后即可搜索与投票，无需安装 App。

## ✨ 核心特性

- **跨平台主机端**：.NET MAUI + XAML（Android / Windows，MacCatalyst 可保留）
- **MVVM 分层**：XAML 视图 + ViewModel + Services，便于测试与维护
- **局域网访客端**：纯 HTML/CSS/JS 单页应用（`Resources/Raw/WebClient`）
- **Spotify OAuth 2.0 PKCE**：安全登录，无需 Client Secret
- **实时投票队列**：多访客并发投票，按票数与时间排序
- **自动队列同步**：将高票歌曲自动推进 Spotify 播放队列
- **SQLite 持久化**：历史记录与投票记录落库
- **QR + PIN 入场**：扫码即用，避免 URL 手输

## 🧩 系统工作流

1. 主机启动应用并完成 Spotify 登录
2. 应用启动本地 GenHTTP 服务，生成访问 URL、QR 码和 6 位 PIN
3. 访客连接同一 WiFi，扫码进入网页并输入 PIN
4. 访客搜索歌曲并投票/取消投票
5. 主机按投票结果维护队列，并同步至 Spotify

## 🏗️ 技术架构

```
主机（.NET MAUI）                               访客（浏览器）
┌────────────────────────────────────────┐     ┌──────────────────────────┐
│  AppShell / MVVM                      │◄──►│  WebClient (SPA)          │
│  ├─ SpotifyAuthService (PKCE)         │ WiFi│  ├─ PIN 登录              │
│  ├─ SpotifyApiService                 │     │  ├─ 搜索/投票             │
│  ├─ SessionManager + VotingEngine     │     │  └─ 队列/当前播放         │
│  ├─ QueueSyncService                  │     └──────────────────────────┘
│  ├─ DatabaseService (SQLite)          │
│  └─ WebServerService (GenHTTP)        │
│      ├─ Endpoints (/api/*)            │
│      └─ Static files (WebClient)      │
└────────────────────────────────────────┘
```

## 🔧 当前技术基线

- **目标框架**：`net10.0-android`、`net10.0-windows10.0.19041.0`
- **嵌入式服务**：`GenHTTP.Core` + `GenHTTP.Modules.*` 提供局域网 API 与静态页面托管
- **状态管理**：主机端采用 `XAML + MVVM`（CommunityToolkit.Mvvm）分层
- **二维码生成**：使用 `QRCoder` 输出 PNG 字节流并在 MAUI 中渲染
- **配置与存储**：应用配置使用 `Preferences`，投票与历史记录使用 `SQLite`（`sqlite-net-pcl`）
- **访客端形态**：`Resources/Raw/WebClient` 中的纯 HTML/CSS/JS 单页应用

## ⚙️ Spotify 配置

1. 打开 [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. 创建应用并设置 Redirect URI：`sharedjoy://callback`
3. 记录 `Client ID`
4. 在 App 设置页输入并保存 `Client ID`
5. 点击“登录 Spotify”完成授权
