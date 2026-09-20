# HolyHand — Port Status

> Living feature-parity matrix. Updated at the end of every milestone.
> Status: `not started` | `in progress` | `done (tested)` | `partial` | `blocked`

## Upstream Capabilities

| # | Capability | Status | Evidence | Notes |
|---|---|---|---|---|
| 1 | Menu bar / tray icon | not started | — | WPF + H.NotifyIcon.Wpf |
| 2 | Hotkey trigger (Ctrl+Space → Ctrl+Win) | not started | — | Low-level hook + state machine |
| 3 | Text input overlay/popup | not started | — | WPF borderless topmost window |
| 4 | Status indicator (floating) | not started | — | WPF floating panel |
| 5 | Target app capture | not started | — | GetForegroundWindow + process info |
| 6 | Accessibility tree walking | not started | — | FlaUI.UIA3 with CacheRequest |
| 7 | Element model (id, role, label, value) | not started | — | C# record/class |
| 8 | Jev API client (action selection) | not started | — | HttpClient + Vercel AI Gateway |
| 9 | Jev text selection (2-phase) | not started | — | Same pattern, different endpoint |
| 10 | Jev completion verification | not started | — | Boolean check via /v1/evaluate |
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
| 23 | API key storage (Keychain → Cred Mgr) | not started | — | Windows Credential Manager |
| 24 | Error detail extraction + key redaction | not started | — | Portable pattern |
| 25 | CDP browser support (Electron) | not started | — | M9, optional |
| 26 | Electron detection | not started | — | M9, optional |

## New Windows Capabilities

| # | Capability | Status | Evidence | Notes |
|---|---|---|---|---|
| W1 | Ctrl+Win modifier-only chord | not started | — | Pure state machine in Core |
| W2 | Start menu suppression | not started | — | VK 0xE8 injection |
| W3 | Fallback hotkey (Ctrl+Alt+Space) | not started | — | RegisterHotKey |
| W4 | Kill switch (second press = cancel) | not started | — | State machine + coordinator |
| W5 | Risk policy (deterministic) | not started | — | IRiskPolicy with sensitive-verb set |
| W6 | Confirmation dialog | not started | — | WPF with action/target/window |
| W7 | Jev risk escalation call | not started | — | Score type via /v1/evaluate |
| W8 | Audit log (JSONL) | not started | — | %LOCALAPPDATA%\HolyHand\audit |
| W9 | Per-app deny-list | not started | — | Config-driven |
| W10 | Elevated target (UIPI) detection | not started | — | Process integrity level check |
| W11 | Voice input (Whisper.net) | not started | — | NAudio + whisper.cpp |
| W12 | Dry-run mode (default) | not started | — | Print actions, execute nothing |
| W13 | Single-instance guard | not started | — | Named Mutex |
| W14 | Per-monitor DPI v2 | not started | — | App manifest |
| W15 | System theme following (light/dark) | not started | — | Registry watch or WinRT |
| W16 | First-run API key setup dialog | not started | — | WPF dialog → Credential Manager |
| W17 | CLI diagnostics (check, snapshot, dry-run) | not started | — | HolyHand.Cli project |
| W18 | Self-contained publish (x64 + arm64) | not started | — | ReadyToRun, Inno Setup |
| W19 | GitHub Actions CI | not started | — | windows-latest |
| W20 | Start-at-login toggle | not started | — | Registry or Task Scheduler |
