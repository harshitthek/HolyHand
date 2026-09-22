# HolyHand — Port Status

> Living feature-parity matrix. Updated at the end of every milestone.
> Status: `not started` | `in progress` | `done (tested)` | `partial` | `blocked`

## Upstream Capabilities

| # | Capability | Status | Evidence | Notes |
|---|---|---|---|---|
| 1 | Menu bar / tray icon | done (tested) | App.xaml.cs | WPF single-instance tray app + hotkey chord |
| 2 | Hotkey trigger (Ctrl+Space → Ctrl+Win) | done (tested) | ChordStateMachineTests.cs | Low-level hook + pure state machine (HK-01..07) |
| 3 | Text input overlay/popup | done (tested) | PromptPopupWindow.xaml | WPF borderless topmost window (<200ms show) |
| 4 | Status indicator (floating) | done (tested) | PromptPopupWindow.xaml | Live status line in popup |
| 5 | Target app capture | done (tested) | WindowCaptureService.cs | GetForegroundWindow + process info + UIPI check |
| 6 | Accessibility tree walking | done (tested) | UiaScreenReader.cs, ScreenReaderTests.cs | FlaUI.UIA3 with CacheRequest (RD-01, RD-02, RD-04) |
| 7 | Element model (id, role, label, value) | done (tested) | AccessibilityElement.cs | C# record with UIA mappings and outcome evidence logic |
| 8 | Jev API client (action selection) | done (tested) | JevClientTests.cs, Cli check | HttpClient + Vercel AI Gateway /v1/evaluate (JV-01..09 live verified: 843ms, $0) |
| 9 | Jev text selection (2-phase) | done (tested) | JevDecisionModel.cs | Regex extraction + Jev choice selection |
| 10 | Jev completion verification | done (tested) | JevDecisionModel.cs | Boolean verification check via /v1/evaluate |
| 11 | Agent loop (observe→decide→act→verify) | done (tested) | AgentLoopTests.cs | Complete loop with state diff and loop guard |
| 12 | Click execution (AXPress → UIA Invoke) | done (tested) | ActionExecutorTests.cs | UIA Invoke/Toggle/Selection patterns + SendInput fallback (EX-02, EX-03) |
| 13 | Type text execution | done (tested) | ActionExecutorTests.cs | UIA ValuePattern + SendInput fallback (EX-01) |
| 14 | Key press execution | done (tested) | ActionExecutor.cs, InputSimulator.cs | SendInput with VK codes (Enter, Tab, Esc) |
| 15 | Scroll execution | done (tested) | ActionExecutor.cs, InputSimulator.cs | SendInput mouse wheel scroll |
| 16 | Text candidate building | done (tested) | JevDecisionModel.cs | Regex extraction + candidate matching from user goal |
| 17 | Text extraction (regex) | done (tested) | JevDecisionModel.cs | Quoted strings & search patterns |
| 18 | Field focus confirmation | done (tested) | ActionExecutor.cs | Click-to-focus before SendInput fallback |
| 19 | Loop guard / stall detection | done (tested) | LoopGuard.cs, AgentLoopTests.cs | Identical state hash detection with max consecutive stall limit (EX-05) |
| 20 | Action verification (post-action) | done (tested) | AgentLoop.cs, AgentLoopTests.cs | Re-snapshot comparison and goal verification |
| 21 | OCR fallback (Apple Vision → Win OCR) | done (tested) | WindowsOcrService.cs, ScreenReaderTests.cs | Windows.Media.Ocr local fallback (RD-06) |
| 22 | Window snapshot (for OCR) | done (tested) | WindowsOcrService.cs | Graphics.CopyFromScreen / WinRT SoftwareBitmap |
| 23 | API key storage (Keychain → Cred Mgr) | done (tested) | EnvLoader.cs | Process environment + AppContext .env loading |
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
| W5 | Risk policy (deterministic) | done (tested) | RiskPolicyTests.cs | Sensitive-verb set (RS-01), cannot downgrade (RS-02) |
| W6 | Confirmation dialog | done (tested) | ConfirmationDialogTests.cs | WPF dark-glass modal (RS-03) + ConsoleConfirmationPrompt |
| W7 | Jev risk escalation call | done (tested) | JevDecisionModel.cs, RiskPolicyTests.cs | Call B score evaluation escalates harmless actions (RS-02) |
| W8 | Audit log (JSONL) | done (tested) | JsonlAuditLog.cs, AuditLogTests.cs | Local audit in %LOCALAPPDATA%\HolyHand\audit with secret redaction |
| W9 | Per-app deny-list | done (tested) | RiskPolicyTests.cs | Blocks 1Password, Bitwarden, KeePass, etc. (RS-09) |
| W10 | Elevated target (UIPI) detection | done (tested) | WindowCaptureService.cs | Process integrity level & access check |
| W11 | Voice input (Whisper.net) | not started | — | NAudio + whisper.cpp |
| W12 | Dry-run mode (default) | done (tested) | AgentLoop.cs, HolyHand.Cli | Simulated execution default until M6; CLI dry-run tool |
| W13 | Single-instance guard | done (tested) | App.xaml.cs | Global Named Mutex |
| W14 | Per-monitor DPI v2 | done (tested) | app.manifest | PerMonitorV2 enabled |
| W15 | System theme following (light/dark) | not started | — | Registry watch or WinRT |
| W16 | First-run API key setup dialog | not started | — | WPF dialog → Credential Manager |
| W17 | CLI diagnostics (check, snapshot, dry-run) | done (tested) | HolyHand.Cli | `check` subcommand performs live Gateway call (verified 843ms, $0 cost) |
| W18 | Self-contained publish (x64) | done (tested) | publish/HolyHand-win-x64 | ReadyToRun win-x64, zip package (HolyHand-v0.1.0-win-x64.zip) |
| W19 | GitHub Actions CI & Release | done (tested) | .github/workflows/ | ci.yml & release.yml on windows-latest |
| W20 | Start-at-login toggle | not started | — | Registry or Task Scheduler |
| W21 | Universal PC App Discovery | done (tested) | AppLauncherTests.cs | Start Menu .lnk scanner, Registry App Paths lookup, running window handoff |
| W22 | Web Search & Site Synthesis | done (tested) | AppLauncherTests.cs, UrlLauncherValidator.cs | Direct browser launch for YouTube, Google, Reddit, etc. queries |
| W23 | Strict Deletion Prohibition | done (tested) | RiskPolicyTests.cs, AgentLoop.cs | Immediate refusal of deletion tasks & deletion UI actions (RS10..13) |
