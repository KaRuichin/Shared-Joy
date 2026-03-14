# Plan: 众乐 (Shared Joy) — .NET MAUI 全平台开发计划

## TL;DR

构建一个 .NET MAUI + XAML 跨平台应用（Android/Windows），将设备变为本地 Spotify 点唱机。主机运行嵌入式 Web 服务器，访客通过浏览器扫码投票选歌，主机自动将得票最高歌曲推送至 Spotify 播放队列。采用 **XAML + MVVM** 架构 + GenHTTP 嵌入式服务器 + SQLite 持久化 + 纯 HTML/CSS/JS 访客端。

代码中的注释使用简体中文，README 和文档也以中文撰写。

> **⚠️ UI 语言说明**
>
> 应用面向英文用户，**所有用户可见的 UI 文本（XAML 页面、访客 Web 端）均使用英文**。
> 代码注释和项目文档（plan.md / README）保持中文。

<!-- -->

> **⚠️ 技术审查说明（2026-03-11）**
>
> 本计划已通过 NuGet 官网逐一验证所有依赖的最新版本和维护状态，做出以下修正：
>
> 1. **目标框架 net9.0 → net10.0** — .NET 9 (STS) 将于 2026 年 5 月终止支持；.NET 10 (LTS) 已于 2025 年 11 月发布，`CommunityToolkit.Maui` v14.0.1 已仅支持 net10.0+
> 2. **移除 `SkiaSharp.Views.Maui.Controls`** — QRCoder v1.7.0 的 `PngByteQRCode` 可直接输出 PNG byte[]，通过 `ImageSource.FromStream()` 即可渲染，无需引入 SkiaSharp（~20MB 原生依赖）
> 3. **移除 `AppSettings.cs` 模型和 SQLite `Settings` 表** — MAUI 内置 `Preferences` API 已完全覆盖键值配置存储需求，无需额外表
> 4. **移除显式 `SQLitePCLRaw.bundle_green`** — 已由 `sqlite-net-pcl` v1.9.172 自动包含为传递依赖
> 5. **将嵌入式服务器改为 `GenHTTP`** — 采用 `GenHTTP.Core` v10.5.0（2026/02 活跃更新），替代旧的维护停滞方案

---

## 项目结构

```text
Shared Joy/
├── Shared Joy.slnx
├── Shared Joy.csproj                   # 单项目 MAUI 工程文件
├── App.xaml / App.xaml.cs
├── MauiProgram.cs                      # DI 注册入口
├── AppShell.xaml                       # Shell 导航
│   │
├── Models/
│   ├── SpotifyTrack.cs                 # Spotify 歌曲模型
│   ├── VoteItem.cs                     # 投票项（歌曲 + 票数 + 时间戳）
│   ├── GuestSession.cs                 # 访客会话模型
│   └── PlaybackState.cs                # 播放状态模型
│   │
├── Services/
│   ├── ISpotifyAuthService.cs          # Spotify 认证接口
│   ├── SpotifyAuthService.cs           # OAuth 2.0 PKCE 认证实现
│   ├── ISpotifyApiService.cs           # Spotify API 接口
│   ├── SpotifyApiService.cs            # 搜索/播放控制/队列管理
│   ├── IWebServerService.cs            # 嵌入式服务器接口
│   ├── WebServerService.cs             # GenHTTP 服务器实现
│   ├── IStaticWebAssetService.cs       # Web 静态资源解包接口
│   ├── StaticWebAssetService.cs        # 将 Resources/Raw/WebClient 解包到可访问目录
│   ├── IVotingEngine.cs                # 投票引擎接口
│   ├── VotingEngine.cs                 # 投票逻辑（排序/去重/同步）
│   ├── ISessionManager.cs              # 会话管理接口
│   ├── SessionManager.cs               # PIN 生成/验证/访客追踪
│   ├── IQueueSyncService.cs            # 队列同步接口
│   ├── QueueSyncService.cs             # 定时将高票歌曲推送至 Spotify
│   └── IDatabaseService.cs / DatabaseService.cs  # SQLite 数据访问
│   │
├── ViewModels/
│   ├── DashboardViewModel.cs           # 主面板 VM
│   ├── SettingsViewModel.cs            # 设置 VM
│   └── QueueManagementViewModel.cs     # 队列管理 VM
│   │
├── Pages/
│   ├── DashboardPage.xaml              # 主面板（当前曲目/QR码/PIN/队列）
│   ├── SettingsPage.xaml               # Spotify 配置 + 应用设置
│   └── QueueManagementPage.xaml        # 队列管理（手动调整/移除/锁定）
├── Views/                              # 可复用控件（ContentView/自定义组件）
│   │
├── WebHost/
│   ├── Endpoints/
│   │   ├── AuthEndpoint.cs             # PIN 验证 API
│   │   ├── SearchEndpoint.cs           # Spotify 搜索代理 API
│   │   ├── VoteEndpoint.cs             # 投票/取消投票 API
│   │   ├── QueueEndpoint.cs            # 队列查看 API
│   │   └── NowPlayingEndpoint.cs       # 当前播放 API
│   └── Middleware/
│       ├── GuestSessionGuard.cs        # 访客会话校验
│       └── RateLimitGuard.cs           # PIN 与搜索限流
│   │
├── Resources/
│   ├── Raw/
│   │   └── WebClient/                  # 访客静态资源（MauiAsset）
│   │       ├── index.html              # 访客 SPA 入口
│   │       ├── style.css               # 样式
│   │       └── app.js                  # 客户端逻辑
│   ├── Images/
│   ├── Fonts/
│   └── Styles/
│   │
├── Helpers/
│   ├── QrCodeGenerator.cs              # QR 二维码生成
│   ├── NetworkHelper.cs                # 获取设备局域网 IP
│   └── PinGenerator.cs                 # 6 位 PIN 码生成
│   │
└── Platforms/
   ├── Android/
   │   └── AndroidManifest.xml         # 网络权限 + Deep Link
   ├── MacCatalyst/                    # 模板默认目标（可保留）
   └── Windows/
      └── Package.appxmanifest        # 协议注册
```

---

## 技术栈 & NuGet 依赖

| 包名 | 用途 |
| --- | --- |
| `CommunityToolkit.Mvvm` (v8.4.0) | MVVM 基础设施（ObservableObject, RelayCommand 等） |
| `CommunityToolkit.Maui` (v14.0.1) | MAUI 扩展控件与行为 |
| `GenHTTP.Core` (v10.5.0) | 嵌入式 HTTP 服务器核心引擎 |
| `GenHTTP.Modules.Webservices` (v10.5.0) | REST 端点定义与 JSON WebService 支持 |
| `GenHTTP.Modules.IO` (v10.5.0) | 静态文件/资源处理能力 |
| `GenHTTP.Modules.Practices` (v10.5.0) | 默认安全与性能实践（压缩、缓存等） |
| `QRCoder` (v1.7.0) | 纯 .NET QR 码生成（PngByteQRCode 输出 byte[] → ImageSource.FromStream） |
| `sqlite-net-pcl` (v1.9.172) | SQLite ORM 持久化（已内含 SQLitePCLRaw 传递依赖） |

**目标框架**: `net10.0-android`, `net10.0-windows10.0.19041.0`

---

## 阶段计划

### Phase 1: 项目脚手架 & 基础配置

> 目标：建立可编译运行的项目骨架

1. 基于现有 `Shared Joy` 单项目 MAUI 工程继续开发（无需重新 `dotnet new maui`），目标平台为 Android / Windows（MacCatalyst 可保留）
2. 安装所有 NuGet 依赖包（当前 `Shared Joy.csproj` 尚未加入计划中的第三方包，此步骤为必做）
3. 搭建文件夹结构（Models / Services / ViewModels / Pages / Views / WebHost / Helpers）
4. 在 `MauiProgram.cs` 中注册 DI 容器（所有服务接口→实现映射）
5. 配置 `AppShell.xaml` 与页面 `*.xaml.cs`，注册三个主页面路由（Dashboard / Settings / QueueManagement）
6. Android: `AndroidManifest.xml` 添加 `INTERNET`、`ACCESS_NETWORK_STATE`、`ACCESS_WIFI_STATE` 权限，以及 `sharedjoy://callback` Intent Filter
7. Windows: `Package.appxmanifest` 注册协议处理

**验证**: 两平台均可编译运行，Shell 导航可在页面间切换

---

### Phase 2: Spotify 认证

> 目标：实现 OAuth 2.0 PKCE 登录流程
> *依赖 Phase 1*

1. 实现 `SpotifyAuthService`：
   - PKCE code_verifier / code_challenge 生成
   - 构造授权 URL：`https://accounts.spotify.com/authorize`，scope 包含 `user-read-playback-state user-modify-playback-state user-read-currently-playing streaming`
   - Android 使用 `WebAuthenticator.AuthenticateAsync()` 拦截回调
   - Windows 使用系统浏览器 + 本地 loopback 监听回调
   - Token exchange：`POST https://accounts.spotify.com/api/token`
   - Token 存储到 `SecureStorage`
   - 自动刷新 Token（refresh_token 流程）
2. 实现 `SettingsPage`：
   - Client ID 输入框 + 保存到 Preferences
   - "登录 Spotify" 按钮触发认证流程
   - 显示当前登录状态（用户名 / 头像）

**验证**: 点击登录 → 跳转 Spotify 授权页 → 授权后回调 → 显示用户信息，重启 App 后 Token 自动恢复

---

### Phase 3: Spotify API 服务

> 目标：封装所有 Spotify Web API 交互
> *依赖 Phase 2*

1. 实现 `SpotifyApiService`（使用 `HttpClient` + Bearer Token）：
   - `SearchTracksAsync(query, limit)` → 调用 `GET /v1/search?type=track`
   - `GetCurrentPlaybackAsync()` → `GET /v1/me/player`
   - `AddToQueueAsync(trackUri)` → `POST /v1/me/player/queue?uri=...`
   - `PlayAsync()` / `PauseAsync()` → `PUT /v1/me/player/play|pause`
   - `SkipNextAsync()` → `POST /v1/me/player/next`
   - `GetQueueAsync()` → `GET /v1/me/player/queue`
   - 自动附加 Auth Header，401 时自动刷新 Token 并重试
2. 定义 `SpotifyTrack` 模型：Id, Name, Artists, AlbumName, AlbumImageUrl, Uri, DurationMs

**验证**: 单元测试或在 Debug 页验证搜索返回结果、获取播放状态

---

### Phase 4: SQLite 数据库

> 目标：持久化存储设置、历史和投票记录
> *可与 Phase 3 并行*

1. 实现 `DatabaseService`：
   - 初始化 SQLite 连接（路径 `FileSystem.AppDataDirectory/sharedjoy.db`）
   - 表结构：
     - `PlayHistory`: Id, TrackId, TrackName, Artists, PlayedAt, SessionId
     - `VoteRecord`: Id, TrackId, GuestId, SessionId, VotedAt
   - 注：应用配置（如 Client ID）使用 MAUI 内置 `Preferences` API 存储，无需 SQLite 表
   - CRUD 方法

**验证**: 写入 → 读取 → 删除流程通过

---

### Phase 5: 会话管理 & 投票引擎

> 目标：PIN 码会话 + 投票排序逻辑
> *依赖 Phase 4*

1. 实现 `SessionManager`：

   - `StartSession()` → 生成 6 位随机 PIN（加密安全随机数），记录会话开始时间
   - `ValidatePin(pin)` → 验证 PIN 正确性
   - `RegisterGuest(identifier)` → 创建/返回 GuestSession
   - `EndSession()` → 清理所有访客和投票数据
   - 可选：限制最大访客数
2. 实现 `VotingEngine`：

   - 内存中维护 `ConcurrentDictionary<string, VoteItem>` （trackId → VoteItem）
   - `Vote(guestId, track)` → 添加投票，同一访客同一歌曲只能投一票
   - `Unvote(guestId, trackId)` → 取消投票
   - `GetRankedQueue()` → 返回按票数降序、同票数按时间升序排列的列表
   - 线程安全（多访客并发投票）
3. 实现 `QueueSyncService`（后台服务）：

   - 定时轮询（每 5-10 秒）检查 Spotify 当前播放状态
   - 当播放队列即将耗尽时，自动取排名第一的歌曲添加到 Spotify 队列
   - 已添加的歌曲从投票列表移除，记录到 PlayHistory

**验证**: 多个模拟访客投票 → 队列正确排序 → 歌曲自动推送到 Spotify

---

### Phase 6: 嵌入式 Web 服务器 & REST API

> 目标：在设备上启动 HTTP 服务器，提供访客 API
> *依赖 Phase 5*

1. 实现 `WebServerService`：

   - 使用 GenHTTP 创建服务器 Host
   - 监听 `http://*:{port}/`（端口可配，默认 8080）
   - 路由注册：
     - 静态内容处理器 → 托管解包后的 `Resources/Raw/WebClient/` 静态文件
     - API 处理器 → 注册 REST 路由（auth/search/vote/queue/now-playing）
   - 提供 `Start()` / `Stop()` 生命周期管理
2. 实现 Web API 端点（GenHTTP 路由/处理器风格）：

   - `POST /api/auth` — 请求体 `{ pin }` → 验证 PIN，返回会话 cookie/token
   - `GET /api/search?q={query}` — 代理到 Spotify 搜索（需已认证）
   - `POST /api/vote/{trackId}` — 请求体含歌曲信息，记录投票
   - `DELETE /api/vote/{trackId}` — 取消投票
   - `GET /api/queue` — 返回当前投票排行队列
   - `GET /api/now-playing` — 返回 Spotify 当前播放状态
   - 所有 API（除 /api/auth 外）需验证访客会话有效
3. 网络辅助：

   - `NetworkHelper.GetLocalIpAddress()` → 获取设备局域网 IP（考虑多网卡场景）

**安全要点**：

- PIN 验证防暴力破解（限制尝试次数 + 延迟）
- API 路由仅限局域网访问
- 输入验证：搜索词长度限制、trackId 格式校验
- 会话 Token 使用加密安全随机数生成

**验证**: 浏览器手动访问 `http://{ip}:8080/` 加载页面，API 返回正确 JSON

---

### Phase 7: Web 客户端（访客界面）

> 目标：访客使用的纯 HTML/CSS/JS 单页应用
> *依赖 Phase 6 的 API 定义*

1. `index.html` — 单页应用骨架：

   - PIN 输入视图（初始视图）
   - 主界面：搜索栏 + 搜索结果 + 投票队列 + 当前播放
   - 响应式设计（移动端优先）
2. `style.css` — 样式：

   - 深色主题（与 Spotify 风格呼应）
   - 移动端适配（触控友好的按钮大小）
3. `app.js` — 客户端逻辑：

   - PIN 提交 → `POST /api/auth`，存储会话 Token
   - 搜索输入 → 防抖 300ms → `GET /api/search?q=...`
   - 投票/取消 → `POST/DELETE /api/vote/{trackId}`
   - 轮询（每 3 秒）刷新队列和当前播放
   - 投票动画/反馈

**验证**: 手机浏览器完整走通：输入 PIN → 搜索 → 投票 → 看到队列更新

---

### Phase 8: 主机 Dashboard 界面

> 目标：主机端展示完整控制面板
> *依赖 Phase 2, 5, 6*

1. `DashboardPage.xaml` + `DashboardViewModel.cs`：

   - 顶部区域：当前播放歌曲（封面 / 歌名 / 艺术家 / 进度条）
   - 播放控制按钮：播放/暂停、下一首
   - 中部区域：投票队列列表（实时更新，显示歌名 + 票数）
   - 侧边/底部区域：
     - QR 码图片（使用 QRCoder 生成当前服务器地址）
     - 6 位 PIN 码大字体显示
     - 在线访客数
   - "开始会话" / "结束会话" 按钮
2. `QrCodeGenerator`：

   - 输入 URL → 输出 ImageSource（通过 QRCoder 生成 PNG → Stream → ImageSource）

**验证**: 启动会话后 QR 码可扫描，队列实时更新

---

### Phase 9: 队列管理

> 目标：为主持人提供手动队列干预能力
> *依赖 Phase 5, 8*

1. `QueueManagementPage.xaml` + `QueueManagementViewModel.cs`：
   - 展示当前投票队列与已推送状态（待播/已入 Spotify 队列）
   - 支持手动置顶、下移、移除歌曲
   - 支持“锁定下一首”以临时覆盖自动票数排序
   - 支持一键清空待投票队列（保留已播放历史）
2. 与 `QueueSyncService` 联动：
   - 存在“锁定下一首”时优先推送该歌曲
   - 手动移除后同步更新内存投票池与持久化记录

**验证**: 主持人可在队列管理页完成置顶/移除/锁定操作，且 Spotify 入队顺序符合预期

---

### Phase 10: 测试 & 收尾

> 目标：全面测试 + 异常处理 + 打磨体验

1. 异常处理：

   - Spotify Token 过期时的优雅降级
   - 网络断开时的提示（主机和访客）
   - 无活跃 Spotify 设备时的提示
   - Web 服务器端口被占用时的 fallback
2. 跨平台测试：

   - Android 真机：WiFi 环境下完整流程
   - Windows：本地开发调试流程
3. 性能：

   - 访客并发压测（10+ 同时投票）
   - 内存泄漏检查（长时间运行会话）
4. UX 打磨：

   - 应用图标 & 启动画面
   - 暗色/亮色主题
   - 错误提示 toast

---

## 关键文件清单

| 文件路径 | 说明 |
| --- | --- |
| `MauiProgram.cs` | DI 注册所有服务 |
| `Services/SpotifyAuthService.cs` | PKCE 全流程 + Token 管理 |
| `Services/SpotifyApiService.cs` | Spotify Web API 封装 |
| `Services/WebServerService.cs` | GenHTTP 服务器生命周期管理 |
| `Services/StaticWebAssetService.cs` | 访客静态资源解包 |
| `Services/VotingEngine.cs` | 线程安全投票排序核心逻辑 |
| `Services/SessionManager.cs` | PIN + 访客会话 |
| `Services/QueueSyncService.cs` | 后台同步 Spotify 队列 |
| `Services/DatabaseService.cs` | SQLite CRUD |
| `WebHost/Endpoints/*.cs` | REST API 端点定义（5 个） |
| `Resources/Raw/WebClient/index.html` | 访客 SPA |
| `Resources/Raw/WebClient/app.js` | 访客端逻辑 |
| `Pages/DashboardPage.xaml` | 主机控制面板 |
| `Pages/SettingsPage.xaml` | Spotify 配置 |
| `Pages/QueueManagementPage.xaml` | 主持人队列管理 |
| `Helpers/NetworkHelper.cs` | 局域网 IP 检测 |
| `Helpers/QrCodeGenerator.cs` | QR 码生成 |

---

## 架构决策

| 决策 | 选择 | 理由 |
| --- | --- | --- |
| 嵌入式服务器 | GenHTTP.Core | 活跃维护（v10.5.0），纯 .NET 实现，支持嵌入式托管与路由扩展 |
| MVVM 框架 | CommunityToolkit.Mvvm | 官方推荐、源生成器、轻量 |
| QR 生成 | QRCoder | 纯 .NET 实现，无平台特定依赖 |
| 认证流程 | PKCE (无 Client Secret) | 移动端/桌面端安全最佳实践，无需后端 |
| 实时更新 | 轮询（3s 间隔） | 比 WebSocket 简单，局域网延迟可接受 |
| Web 客户端 | 纯 HTML/CSS/JS | 零构建依赖，嵌入 MAUI 资源包即可 |
| 数据库 | SQLite (sqlite-net-pcl) | 轻量、跨平台、ORM 友好 |
| 目标平台 | Android + Windows（MacCatalyst 保留模板默认） | 与当前单项目 MAUI 模板一致，首发聚焦 Android + Windows |
| UI 语言 | 英文 | 面向英文用户；代码注释与文档保持中文 |

---

## UI 技术路线（MAUI + XAML）

- 主机端 UI 统一使用 **.NET MAUI XAML 页面**（`Pages/*.xaml`）+ `ViewModels/*.cs`
- `Views/` 仅用于可复用 UI 组件（如 `SongCardView.xaml`、`QueueItemView.xaml`）
- 所有界面状态通过 MVVM 绑定（`ObservableObject` / `RelayCommand`）驱动
- 保持 `XAML + CodeBehind + ViewModel` 分层：
  - `*.xaml`：布局与样式
  - `*.xaml.cs`：生命周期与轻量 UI 事件桥接
  - `ViewModel`：业务状态、命令与服务调用
- 不引入 Blazor Hybrid / WebView 作为主机端 UI 主路线（访客端网页仅用于局域网投票）

---

## 注意事项

1. **Spotify Premium 要求**: Spotify Web API 的播放控制功能要求用户有 Premium 账户，需在 UI 中提示
2. **Windows 防火墙**: Windows 上首次启动服务器可能触发防火墙提示，需引导用户允许
3. **Spotify API Rate Limit**: 搜索和播放控制有速率限制，需在 API 层做防抖和限流
4. **GenHTTP 选型说明**: 采用 `GenHTTP.Core` (v10.5.0, MIT, 纯 .NET 实现) 作为嵌入式服务器，建议继续通过 `IWebServerService` 接口隔离实现，便于后续替换或 A/B 评估
5. **SkiaSharp 不再需要**: QRCoder 的 `PngByteQRCode` 可直接生成 PNG 字节数组，使用 `ImageSource.FromStream(new MemoryStream(pngBytes))` 即可在 MAUI Image 控件中显示，避免引入 SkiaSharp 的 ~20MB 原生依赖
6. **.NET 版本选择**: 使用 .NET 10 LTS（2025/11 发布，支持至 2028/11）。.NET 9 STS 将于 2026/05 终止支持，不适合新项目
