# TeleDrive

A personal cloud storage desktop application that uses Telegram as its backend. Upload, download, and manage any file type through a clean modern interface — with automatic chunking for large files, cross-device access, and zero subscription costs.

---

## What It Is

TeleDrive turns your Telegram account or bot into a private cloud drive. Files are stored as messages in a dedicated private channel. A JSON index message in that same channel tracks every file, its chunks, and its metadata — meaning any device running TeleDrive can immediately see your full library with no server required.

---

## Key Features (Planned)

- **Dual connection modes** — Bot API (simple setup) or MTProto user account (2GB per file)
- **Automatic chunking** — Files larger than the API limit are split, uploaded, and reassembled transparently
- **Cross-device access** — Index lives in Telegram; any authenticated device sees the same library
- **Multi-threaded transfers** — Parallel uploads/downloads with per-file progress, speed, and ETA
- **Explorer-style UI** — Folders, search, sort, grid/list view
- **Setup wizard** — Guided 3-step onboarding for both connection modes
- **Themes** — Dark, Light, System (default)
- **Local cache** — SQLite mirror of the index for instant browse without hitting Telegram
- **Resumable transfers** — Failed chunk uploads retry independently; interrupted sessions resume
- **Future: Android and iOS** — Core logic is platform-agnostic; mobile ports share the same library

---

## Tech Stack

| Layer | Technology |
|---|---|
| Windows UI | WPF on .NET 8, MVVM via CommunityToolkit.Mvvm |
| Bot API transport | Telegram.Bot |
| MTProto transport | WTelegramClient |
| Local index cache | Microsoft.Data.Sqlite |
| Serialization | System.Text.Json |
| Future mobile UI | .NET MAUI (Android + iOS) |
| Shared core | .NET 8 class library (TeleDrive.Core) |

---

## Solution Structure

```
TeleDrive/
├── TeleDrive.sln
├── TeleDrive.Core/                      # Platform-agnostic business logic
│   ├── Interfaces/
│   │   ├── ITelegramService.cs          # Upload, download, delete, test connection
│   │   ├── IChunkingService.cs          # Split, reassemble, hash
│   │   └── IIndexService.cs             # Read/write file index from Telegram
│   ├── Models/
│   │   ├── VaultFile.cs                 # A file entry in the index
│   │   ├── FileChunk.cs                 # One chunk of a split file
│   │   ├── TransferItem.cs              # A live upload or download operation
│   │   ├── AppSettings.cs               # Persisted user settings
│   │   └── ConnectionMode.cs            # BotApi | MtProto enum
│   ├── Services/
│   │   ├── BotApiTelegramService.cs     # ITelegramService via Telegram.Bot
│   │   ├── MtProtoTelegramService.cs    # ITelegramService via WTelegramClient
│   │   ├── ChunkingService.cs           # Memory-mapped chunking + SHA-256
│   │   ├── IndexService.cs              # Read/write JSON index in Telegram channel
│   │   └── TransferOrchestrator.cs      # Bounded work queue + worker pool
│   └── Helpers/
│       ├── HashHelper.cs                # SHA-256 helpers
│       └── SettingsHelper.cs            # Load/save AppSettings to disk
│
└── TeleDrive.WPF/                       # Windows presentation layer
    ├── App.xaml / App.xaml.cs
    ├── Themes/
    │   ├── Dark.xaml
    │   ├── Light.xaml
    │   └── ThemeManager.cs
    ├── Views/
    │   ├── Wizard/
    │   │   ├── WizardWindow.xaml        # Host window with step indicator
    │   │   └── Pages/
    │   │       ├── WelcomePage.xaml
    │   │       ├── ChooseModePage.xaml
    │   │       ├── BotApiSetupPage.xaml
    │   │       ├── MtProtoSetupPage.xaml
    │   │       ├── StoragePage.xaml
    │   │       └── DonePage.xaml
    │   ├── MainWindow.xaml              # Shell: sidebar + content area
    │   └── Pages/
    │       ├── FilesPage.xaml           # Explorer-style file browser
    │       ├── TransfersPage.xaml       # Active/completed transfers
    │       └── SettingsPage.xaml        # Theme, transfer limit, cache
    ├── ViewModels/
    │   ├── Wizard/
    │   │   ├── WizardViewModel.cs       # Step navigation, shared wizard state
    │   │   ├── WelcomePageViewModel.cs
    │   │   ├── ChooseModePageViewModel.cs
    │   │   ├── BotApiSetupPageViewModel.cs
    │   │   ├── MtProtoSetupPageViewModel.cs
    │   │   ├── StoragePageViewModel.cs
    │   │   └── DonePageViewModel.cs
    │   ├── MainViewModel.cs
    │   ├── FilesPageViewModel.cs
    │   ├── TransfersPageViewModel.cs
    │   └── SettingsPageViewModel.cs
    └── Converters/
        └── CommonConverters.xaml
```

---

## Roadmap

### Phase 1 — Foundation (Core library skeleton) ✅ Completed
Set up the solution, project files, and all interfaces, models, and empty service stubs. No Telegram calls yet — just the contracts everything else will implement.

**Deliverables:**
- `TeleDrive.sln`
- `TeleDrive.Core.csproj` with NuGet references
- `TeleDrive.WPF.csproj` with NuGet references
- All interfaces (`ITelegramService`, `IChunkingService`, `IIndexService`)
- All models (`VaultFile`, `FileChunk`, `TransferItem`, `AppSettings`, `ConnectionMode`)
- `HashHelper.cs`, `SettingsHelper.cs`
- Empty service stubs with XML doc comments

---

### Phase 2 — Core Services (Business logic) ✅ Completed (unverified — not yet compiled)
Implement all three services fully. This is the engine of the app — no UI yet.

**Deliverables:**
- `BotApiTelegramService.cs` — upload document, download file, delete message, send text, test connection (getMe)
- `MtProtoTelegramService.cs` — same interface via WTelegramClient; handles phone auth flow, 2FA
- `ChunkingService.cs` — memory-mapped file split into N-byte chunks, SHA-256 per chunk and whole file, reassembly by streaming chunks into target path at correct offsets
- `IndexService.cs` — reads index JSON from a known pinned message in the vault channel; writes updated index back; local SQLite cache that rebuilds on first launch or on demand
- `TransferOrchestrator.cs` — `System.Threading.Channels` bounded queue, `SemaphoreSlim` worker pool (configurable concurrency), per-file `IProgress<T>` reporting, retry with exponential backoff per chunk

---

### Phase 3 — Setup Wizard (WPF) ✅ Completed (unverified — not yet compiled)
Build the first-run experience. This is the user's first impression and must be flawless.

**Deliverables:**
- `WizardWindow.xaml` — frameless window, step progress indicator at top, Next/Back/Skip nav
- `WelcomePage` — logo, tagline, single Get Started button
- `ChooseModePage` — two large cards: Simple (Bot API) vs Power (MTProto), with feature comparison
- `BotApiSetupPage` — inline 3-step guide with illustrated panels, token input, live validation (calls `ITelegramService.TestConnectionAsync`)
- `MtProtoSetupPage` — step-by-step guide (Open browser → api_id + api_hash inputs → phone number → OTP 5-box input → optional 2FA password)
- `StoragePage` — default: auto-create channel; secondary: paste existing channel ID/link; shows live status as channel is created
  - ⚠️ Auto-create only works in MTProto mode. Bot API cannot programmatically create Telegram channels — Bot API users must pre-create a channel and add the bot as admin.
- `DonePage` — success animation, summary, Open TeleDrive button
- All pages wired to ViewModels with full validation and error display

See [`Project_plans.md`](./Project_plans.md) for the full list of known gaps and unverified areas.

---

### Phase 4 — Main UI (WPF shell + file browser) ✅ Completed (unverified — not yet compiled)
Build the main application window that users spend all their time in.

**Deliverables:**
- `MainWindow.xaml` — sidebar (Files, Transfers), content host via `DataTemplate`-switched `ContentControl`
- `FilesPage.xaml` — virtualized `ListView` of `VaultFile` items, drag-and-drop upload zone from Explorer
- `FilesPageViewModel.cs` — loads local cache first, refreshes from `IIndexService`, triggers uploads via `TransferOrchestrator`, per-row Download button
- `TransfersPage.xaml` — live transfer cards showing file name, progress bar, byte counts, status, pause/resume/cancel buttons
- `TransfersPageViewModel.cs` — bound to `ObservableCollection<TransferItem>`, updated via a shared `IProgress<TransferItem>` from the orchestrator
- Drag-and-drop from Windows Explorer into the files page triggers upload immediately
- `App` now opens `MainWindow` directly on startup if setup is already complete, otherwise the wizard — which now opens `MainWindow` on completion instead of just closing

Not yet built: top bar search/connection indicator and the Settings nav entry — those land with Phase 5's `SettingsPage`.

---

### Phase 5 — Themes + Settings ✅ Completed (unverified — not yet compiled)
Polish the visual layer and expose configuration.

**Deliverables:**
- `Light.xaml` — full color/style coverage mirroring `Dark.xaml`'s keys
- `ThemeManager.cs` — detects Windows system theme via the `AppsUseLightTheme` registry value, swaps the active `ResourceDictionary` at runtime, applied on startup and whenever the user changes the selector
- `SettingsPage.xaml` — connection mode + vault channel (read-only), theme selector, concurrent transfer limit slider, rebuild-cache button
- `SettingsPageViewModel.cs` — persists every change immediately via `SettingsHelper`
- Settings persisted to `%AppData%\TeleDrive\settings.json`

⚠️ Known gap: the concurrent-transfer-limit slider updates `AppSettings` but not the already-running `TransferOrchestrator` worker pool — it takes effect on next launch only. See [`Project_plans.md`](./Project_plans.md).

---

### Phase 6 — Resilience + Polish
Production-quality hardening before any mobile work begins.

**Deliverables:**
- Retry logic in `TransferOrchestrator` with configurable max attempts and backoff
- Resume interrupted sessions: on launch, check for transfers marked `InProgress` in index and offer to resume
- Tray icon with quick-upload and active transfer count badge
- File type icons in the browser (shell icon extraction via Win32 interop)
- Right-click context menu: Download, Delete, Copy Link, Properties
- Keyboard shortcuts: Ctrl+U (upload), Ctrl+F (search), Delete (delete selected)
- Crash reporting to a local log file (`%AppData%\TeleDrive\logs\`)

---

### Phase 7 — Android Port (MAUI)
Add a new `TeleDrive.Android` MAUI project that references `TeleDrive.Core` unchanged.

**Deliverables:**
- `TeleDrive.Android.csproj` (MAUI, net8.0-android)
- MAUI shell with bottom tab bar: Files, Transfers, Settings
- Android-specific `ITelegramService` implementation (TDLib or Bot API HTTP client)
- Setup wizard adapted for mobile UX (full-screen steps, native keyboard handling)
- Background transfer service using Android WorkManager via MAUI

---

### Phase 8 — iOS Port (MAUI)
Add `TeleDrive.iOS` sharing the same Core and most MAUI views from Phase 7.

**Deliverables:**
- `TeleDrive.iOS.csproj` (net8.0-ios)
- iOS-specific permissions (Files, Background fetch)
- Background transfer via iOS URLSession background configuration
- App Store compliance review

---

## Development Notes

### Index message format (stored in Telegram channel)
```json
{
  "version": 1,
  "last_updated": "2026-09-07T12:00:00Z",
  "files": [
    {
      "id": "uuid",
      "name": "archive.zip",
      "size": 2147483648,
      "mime": "application/zip",
      "uploaded": "2026-09-07T12:00:00Z",
      "sha256": "abc123...",
      "chunks": [
        { "index": 0, "message_id": 42, "size": 52428800, "sha256": "..." },
        { "index": 1, "message_id": 43, "size": 52428800, "sha256": "..." }
      ]
    }
  ]
}
```

### Chunk size targets

| Mode | Chunk size |
|---|---|
| Bot API | 49 MB (1 MB headroom under 50 MB limit) |
| MTProto | 1,900 MB (100 MB headroom under 2 GB limit) |

### Threading model
- All Telegram I/O: background `Task` via `async/await`, never blocking UI thread
- Progress updates: `IProgress<TransferProgress>` marshals to UI thread via `Dispatcher`
- Worker pool: `SemaphoreSlim(initialCount: 3)` by default, user-configurable 1–8
- Index writes: serialized through a single `SemaphoreSlim(1)` to avoid race conditions

---

## Getting Started (after build)

1. Clone the repo
2. Open `TeleDrive.sln` in Visual Studio 2022 (17.8+) or Rider
3. Set `TeleDrive.WPF` as startup project
4. Run — the setup wizard launches automatically on first run
5. Follow the wizard (~2 minutes for Bot API, ~5 minutes for MTProto)
6. Start uploading files

---

## Project Plans

See [`Project_plans.md`](./Project_plans.md) for a status/priority tracker of build progress, known gaps, and pending fixes across phases.

---

## License

MIT
