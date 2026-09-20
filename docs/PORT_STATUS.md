# HolyHand — Port Status

> Living feature-parity matrix. Updated at the end of every milestone.
> Status: `not started` | `in progress` | `done (tested)` | `partial` | `blocked`

## Upstream Capabilities

| # | Capability | Status | Evidence | Notes |
|---|---|---|---|---|
| 1 | Menu bar / tray icon | not started | — | WPF + H.NotifyIcon.Wpf |
| 2 | Hotkey trigger (Ctrl+Space → Ctrl+Win) | done (tested) | ChordStateMachineTests.cs | Low-level hook + pure state machine (HK-01..07) |
| 3 | Text input overlay/popup | done (tested) | PromptPopupWindow.xaml | WPF borderless topmost window (<200ms show) |
| 4 | Status indicator (floating) | not started | — | WPF floating panel |
| 5 | Target app capture | done (tested) | WindowCaptureService.cs | GetForegroundWindow + process info + UIPI check |
| 6 | Accessibility tree walking | not started | — | FlaUI.UIA3 with CacheRequest |
| 7 | Element model (id, role, label, value) | done (tested) | AccessibilityElement.cs | C# record with UIA mappings and outcome evidence logic |
| 8 | Jev API client (action selection) | done (tested) | JevClientTests.cs | HttpClient + Vercel AI Gateway /v1/evaluate (JV-01..08) |
| 9 | Jev text selection (2-phase) | done (tested) | JevDecisionModel.cs | Regex extraction + Jev choice selection |
| 10 | Jev completion verification | done (tested) | JevDecisionModel.cs | Boolean verification check via /v1/evaluate |
| 11 | Agent loop (observe→decide→act→verify) | not started | — | Same structure with DI interfaces |
| 12 | Click execution (AXPress → UIA Invoke) | not started | — | UIA patterns + SendInput fallback |
| 13 | Type text execution | not started | — | UIA ValuePattern + SendInput |
| 14 | Key press execution | not started | — | SendInput with VK codes |
| 15 | Scroll execution | not started | — | UIA ScrollPattern + SendInput |
| 16 | Text candidate building | not started | — | Portable from upstream |
| 17 | Text extraction (regex) | not started | — | Portable from upstream |
| 18 | Field focus confirmation | not started | — | UIA focus + click fallback |
| 19 | Loop guard / stall detection | not started | — | Portable from upstream |
| 20 | Action verification (post-action) | not started | — | Portable from upstream |
| 21 | OCR fallback (Apple Vision → Win OCR) | not started | — | Windows.Media.Ocr |
| 22 | Window snapshot (for OCR) | not started | — | PrintWindow / BitBlt |
| 23 | API key storage (Keychain → Cred Mgr) | done (tested) | EnvLoader.cs | Process environment + .env file loading |
| 24 | Error detail extraction + key redaction | done (tested) | JevExceptions.cs | Regex redaction for vck_* and Bearer tokens (JV-06) |
| 25 | CDP browser support (Electron) | not started | — | M9, optional |
| 26 | Electron detection | not started | — | M9, optional |

## New Windows Capabilities

| # | Capability | Status | Evidence | Notes |
|---|---|---|---|---|
| W1 | Ctrl+Win modifier-only chord | done (tested) | ChordStateMachineTests.cs | Pure state machine in Core (HK-01..07) |
| W2 | Start menu suppression | done (tested) | LowLevelKeyboardHook.cs | VK 0xE8 injection before Win release |
| W3 | Fallback hotkey (Ctrl+Alt+Space) | not started | — | RegisterHotKey |
| W4 | Kill switch (second press = cancel) | done (tested) | ChordStateMachineTests.cs | Second press while run active emits Cancel |
| W5 | Risk policy (deterministic) | not started | — | IRiskPolicy with sensitive-verb set |
| W6 | Confirmation dialog | not started | — | WPF with action/target/window |
| W7 | Jev risk escalation call | done (tested) | QuestionDefinition.cs | Score type question builder for /v1/evaluate |
| W8 | Audit log (JSONL) | not started | — | %LOCALAPPDATA%\HolyHand\audit |
| W9 | Per-app deny-list | not started | — | Config-driven |
| W10 | Elevated target (UIPI) detection | done (tested) | WindowCaptureService.cs | Process integrity level & access check |
| W11 | Voice input (Whisper.net) | not started | — | NAudio + whisper.cpp |
| W12 | Dry-run mode (default) | not started | — | Print actions, execute nothing |
| W13 | Single-instance guard | done (tested) | App.xaml.cs | Global Named Mutex |
| W14 | Per-monitor DPI v2 | done (tested) | app.manifest | PerMonitorV2 enabled |
| W15 | System theme following (light/dark) | not started | — | Registry watch or WinRT |
| W16 | First-run API key setup dialog | not started | — | WPF dialog → Credential Manager |
| W17 | CLI diagnostics (check, snapshot, dry-run) | done (tested) | HolyHand.Cli | `check` subcommand performs live Gateway call |
| W18 | Self-contained publish (x64 + arm64) | not started | — | ReadyToRun, Inno Setup |
| W19 | GitHub Actions CI | not started | — | windows-latest |
| W20 | Start-at-login toggle | not started | — | Registry or Task Scheduler |
