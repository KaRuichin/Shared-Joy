# 🎵 Shared Joy

Turn your phone or computer into a **local Spotify jukebox**: the host runs a **.NET MAUI + XAML** app, and guests join from a browser on the same LAN by scanning a QR code and entering a PIN—no app installation required.

## ✨ Key Features

- **Cross-platform host app**: .NET MAUI + XAML (Android / Windows, with optional MacCatalyst support)
- **MVVM architecture**: XAML Views + ViewModels + Services for maintainability and testability
- **LAN guest client**: Pure HTML/CSS/JS SPA (`Resources/Raw/WebClient`)
- **Spotify OAuth 2.0 PKCE**: Secure login without a Client Secret
- **Real-time voting queue**: Concurrent guest voting, sorted by votes and time
- **Automatic queue sync**: Pushes top-voted tracks into Spotify queue automatically
- **SQLite persistence**: Stores history and vote records
- **QR + PIN entry**: Scan to join, no manual URL typing needed

## 🧩 System Workflow

1. The host launches the app and signs in to Spotify.
2. The app starts a local GenHTTP server and generates an access URL, QR code, and a 6-digit PIN.
3. Guests connect to the same Wi-Fi, scan the QR code, and enter the PIN.
4. Guests search tracks and vote/unvote.
5. The host manages the queue by vote results and syncs it to Spotify.

## 🏗️ Technical Architecture

```
Host (.NET MAUI)                                 Guests (Browser)
┌────────────────────────────────────────┐       ┌──────────────────────────┐
│  AppShell / MVVM                      │◄────►│  WebClient (SPA)         │
│  ├─ SpotifyAuthService (PKCE)         │  WiFi │  ├─ PIN Login            │
│  ├─ SpotifyApiService                 │       │  ├─ Search / Vote        │
│  ├─ SessionManager + VotingEngine     │       │  └─ Queue / Now Playing  │
│  ├─ QueueSyncService                  │       └──────────────────────────┘
│  ├─ DatabaseService (SQLite)          │
│  └─ WebServerService (GenHTTP)        │
│      ├─ Endpoints (/api/*)            │
│      └─ Static files (WebClient)      │
└────────────────────────────────────────┘
```

## 🔧 Current Technical Baseline

- **Target frameworks**: `net10.0-android`, `net10.0-windows10.0.19041.0`
- **Embedded server**: `GenHTTP.Core` + `GenHTTP.Modules.*` for LAN APIs and static hosting
- **State architecture**: Host app uses layered `XAML + MVVM` with CommunityToolkit.Mvvm
- **QR generation**: `QRCoder` outputs PNG bytes rendered directly in MAUI
- **Config and persistence**: App settings in `Preferences`; vote/history data in `SQLite` via `sqlite-net-pcl`
- **Guest client**: Pure HTML/CSS/JS SPA under `Resources/Raw/WebClient`

## ⚙️ Spotify Setup

1. Open the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
2. Create an app and set Redirect URI to `sharedjoy://callback`.
3. Copy the `Client ID`.
4. Enter and save the `Client ID` in the app settings page.
5. Click **Log in to Spotify** to complete authorization.
