# HolyHand — Build Log

> Append-only log of every work session, milestone, files created, decisions,
> commands run, test results, and known issues.

---

## 2026-09-21 — M0: Recon and Setup

### Session Start
- **Date**: 2026-09-21
- **Milestone**: M0 — Recon and setup

### Files Read (upstream)
All 23 source files in `Sources/HolyHand/`:
- `HolyHandApp.swift` (23 lines) — SwiftUI @main entry
- `AppDelegate.swift` (267 lines) — App lifecycle, hotkey/overlay/task orchestration
- `HotkeyManager.swift` (89 lines) — CGEvent tap for Ctrl+Space
- `OverlayPanel.swift` (160 lines) — Borderless overlay with text input
- `StatusIndicatorWindow.swift` (131 lines) — Floating status indicator
- `AppTarget.swift` (75 lines) — Frontmost app capture
- `AXTreeWalker.swift` (140 lines) — AX element tree walker
- `AccessibilityElement.swift` (57 lines) — Element model
- `JevClient.swift` (390 lines) — Jev API client (3 call types)
- `TaskRunner.swift` (384 lines) — Agent loop
- `InputController.swift` (87 lines) — CGEvent input (click/type/scroll/key)
- `KeychainHelper.swift` (50 lines) — macOS Keychain CRUD
- `VisionObserver.swift` (78 lines) — Apple Vision OCR
- `WindowSnapshot.swift` (67 lines) — Window capture for OCR
- `RunProgress.swift` (99 lines) — Loop guard and verification
- `TextEntryPlan.swift` (49 lines) — Text candidate building
- `TextExtractor.swift` (20 lines) — Regex text extraction
- `TextFieldFocus.swift` (86 lines) — Focus confirmation before typing
- `CDPClient.swift` (267 lines) — Chrome DevTools Protocol
- `ElectronDetector.swift` (70 lines) — Electron app detection
- `AsyncTimeout.swift` (47 lines) — Task timeout race pattern
- `Logger.swift` (22 lines) — File + stdout logger
- `AgentTypes.swift` (69 lines) — Decision/error/history models

All 6 test files in `Tests/HolyHandTests/`:
- `JevTests.swift` (241 lines) — 12 tests for Jev client
- `ControllerTests.swift` (95 lines) — 9 tests for input/validation
- `RunProgressTests.swift` (149 lines) — 9 tests for loop guard
- `TextEntryPlanTests.swift` (66 lines) — 3 tests for text selection
- `TextFieldFocusTests.swift` (104 lines) — 7 tests for focus handling
- `LocalPerceptionTests.swift` (54 lines) — 3 tests for OCR

Supporting files: `README.md`, `Package.swift`, `LICENSE`, `rebuild.sh`

### Toolchain Check
- **OS**: Windows 11 Home, build 26200 ✅ (exceeds 19041 requirement)
- **Architecture**: x64 ✅
- **Git**: 2.46.1.windows.1 ✅
- **.NET SDK**: ✅ .NET 8.0.425 installed via `dotnet-install.ps1` at `%LOCALAPPDATA%\Microsoft\dotnet`, added to User and session PATH. WPF runtime `Microsoft.WindowsDesktop.App 8.0.31` included.
- **Python**: 3.12.6 ✅
- **Graphify**: 0.9.37 ✅

### Files Created
- `CLAUDE.md` — §2 Ground rules + §7 Safety invariants (verbatim) + graphify section
- `NOTICE` — MIT attribution for Shiv Shanmugam
- `.graphifyignore` — Excludes bin/, obj/, .git/, TestResults/, publish/, *.bin, *.ggml
- `docs/PORT_SPEC.md` — Complete file-by-file mapping and logic specification
- `docs/BUILD_LOG.md` — Append-only build log
- `docs/PORT_STATUS.md` — Feature-parity matrix
- `docs/DECISIONS.md` — Architecture decision records (ADR-001 through ADR-007)

### Key Findings
1. Upstream Jev API uses `api.typesafe.ai/v1/systemone` with `noul` question type
2. Windows port will use `ai-gateway.vercel.sh/v1/evaluate` with `boolean`/`choice`/`score`
3. Upstream has no explicit risk policy — all actions execute without confirmation. Windows port introduces `IRiskPolicy` deterministic safety check first.
4. Upstream uses Ctrl+Space (single key + modifier); Windows uses Ctrl+Win (modifier-only chord requiring low-level keyboard hook state machine and Start menu suppression).
5. Pure logic modules directly portable: RunProgress, TextEntryPlan, TextExtractor, AgentTypes.
6. Every platform module needs full rewrite using Windows APIs (UIA3, WinRT OCR, Credential Manager, SendInput).

### Graphify
- Graphify run completed successfully on upstream repo:
  - **453 nodes, 1015 edges, 17 communities**.
  - **God nodes** (core abstractions):
    1. `AccessibilityElement` (49 edges)
    2. `AppDelegate` (32 edges)
    3. `TaskRunner` (29 edges)
    4. `AgentDecision` (28 edges)
    5. `ControllerError` (27 edges)
    6. `JevClient` (23 edges)
    7. `AppTarget` (21 edges)
    8. `JevTests` (19 edges)
    9. `CDPClient` (16 edges)
    10. `RunProgressTests` (15 edges)
  - **Surprising connections**:
    - `rebuild.sh` → `Scripts/build.sh`
    - `TaskRunner` → `RunProgress`
    - `Observation` → `AccessibilityElement`
    - `JevResult` → `AgentDecision`
    - `TaskRunner` → `ActionHistory`
  - **Language parsing note**: Swift AST parsing produced syntax warnings on 3 files, but fallback extraction captured 453 nodes and 1015 edges accurately.
  - Installed `graphify claude install` hook and appended graphify section to `CLAUDE.md`.

### Known Issues & Next Steps
- Completed M0 recon and user review.
- User noted preference to remove upstream Swift files after full verification at project completion.

---

## 2026-09-21 — M1: Scaffold

### Session Details
- **Date**: 2026-09-21
- **Milestone**: M1 — Solution Scaffold & Architecture Foundation

### Structure Built under `windows/`
- `windows/Directory.Build.props`:
  - `TargetFramework`: `net8.0-windows10.0.19041.0`
  - `Nullable`: `enable`
  - `TreatWarningsAsErrors`: `true`
  - `Deterministic`: `true`
  - `ImplicitUsings`: `enable`
- `windows/Directory.Packages.props`: Central Package Management (CPM) pinning:
  - `Microsoft.Extensions.DependencyInjection` (8.0.1)
  - `Microsoft.Extensions.Logging` (8.0.1)
  - `Microsoft.Windows.CsWin32` (0.3.162)
  - `FlaUI.UIA3` (4.0.0)
  - `H.NotifyIcon.Wpf` (2.1.4)
  - `Serilog` (4.0.2)
  - `xunit` (2.9.2), `FluentAssertions` (6.12.1), `Moq` (4.20.72)
- `windows/NativeMethods.txt`: CsWin32 P/Invoke generation list.
- `windows/HolyHand.sln`: Solution linking all 5 projects.

### Projects Created
1. **`HolyHand.Core`** (Class Library):
   - Zero UI / platform dependencies, pure testable logic.
   - Domain models: `AccessibilityElement`, `AppTarget`, `AgentDecision`, `AgentOperation`, `ActionResult`.
   - Interfaces: `IScreenReader`, `IDecisionModel`, `IActionExecutor`, `IHotkeyService`, `IRiskPolicy`, `IAuditLog`, `ICredentialStore`, `ISpeechInput`, `IClock`.
   - Common utilities: `SystemClock`.
2. **`HolyHand.Platform`** (Class Library):
   - Bridges `Core` interfaces to Windows APIs.
   - P/Invoke generator (`CsWin32`), `FlaUI.UIA3` reference, unsafe code enabled.
3. **`HolyHand.App`** (WPF WinExe):
   - `<UseWPF>true</UseWPF>`, `app.manifest` (PerMonitorV2 DPI awareness, Windows 10/11 compatibility).
   - `App.xaml` + `App.xaml.cs` with single-instance mutex guard and DI container setup.
4. **`HolyHand.Cli`** (Console Exe):
   - Diagnostic tool supporting `check`, `snapshot`, `dry-run` subcommands.
5. **`HolyHand.Tests`** (xUnit):
   - Unit test suite targeting `HolyHand.Core`.

### Verification Results
- `dotnet restore windows/HolyHand.sln`: ✅ All 5 projects restored.
- `dotnet build windows/HolyHand.sln -c Release`: ✅ Succeeded with **0 Warning(s), 0 Error(s)** (Warnings As Errors enforced).
- `dotnet test windows/HolyHand.sln -c Release`: ✅ 1 test passed, 0 failed.
- `dotnet run --project windows/src/HolyHand.Cli -- check`: ✅ Executed successfully.

### Graphify Update
- Ran `graphify update .`:
  - **150 nodes, 171 edges, 34 communities**.
  - **God nodes**: `App` (12), `AccessibilityElement` (9), `Program` (7), `AppTarget` (7), `AgentDecision` (6).
  - **Surprising connections**: `App` --inherits--> `Application`, `SystemClock` --implements--> `IClock`.
  - Updated `graphify-out/GRAPH_REPORT.md`.

### Project Renaming
- Per user instruction, renamed all projects, files, and namespaces from `HolyHand` to `HolyHand` (`HolyHand.sln`, `HolyHand.Core`, `HolyHand.Platform`, `HolyHand.App`, `HolyHand.Cli`, `HolyHand.Tests`).
- Clean build succeeded: 0 warnings, 0 errors.
- Tests passed: 1 passed, 0 failed.
- Graphify re-extracted: 676 nodes, 1247 edges, 30 communities.

---

## 2026-09-21 — M2: Hotkey and Popup

### Session Details
- **Date**: 2026-09-21
- **Milestone**: M2 — Hotkey and Popup

### Components Built
1. **Pure Chord State Machine** (`HolyHand.Core/Hotkey/`):
   - `RawKeyEvent.cs`: Struct representing keydown/keyup, keycode, injected flag, and timestamp.
   - `ChordStateMachine.cs`: Pure state machine for `Ctrl+Win` chord.
     - Detects when both Ctrl and Win are held and one is released without intervening keys.
     - Auto-repeat protection (repeats do not re-arm or double-fire).
     - Intervening key protection (`Ctrl+Win+D` cancels the chord).
     - Injected event protection (`IsInjected = true` is ignored).
     - Kill switch: Triggering while `IsRunActive` emits `OnCancel`. Esc key while running emits `OnCancel`.
2. **Low-Level Keyboard Hook** (`HolyHand.Platform/Hotkey/`):
   - `LowLevelKeyboardHook.cs`: Installs `WH_KEYBOARD_LL` hook on dedicated background thread with Win32 message loop.
   - Non-blocking callback: enqueues to `Channel<RawKeyEvent>` and immediately returns `CallNextHookEx`.
   - Start menu suppression: injects unassigned `VK 0xE8` tap via `SendInput` before Win key release passes through.
3. **Foreground Window Capture & UIPI Check** (`HolyHand.Platform/Windowing/`):
   - `WindowCaptureService.cs`: Captures HWND via `GetForegroundWindow`, queries process ID, process name, window title, and bounds.
   - UIPI elevation check: inspects process access and security token to detect elevated targets (refusing automation with a clear message).
4. **WPF Popup Window** (`HolyHand.App/Windows/`):
   - `PromptPopupWindow.xaml` + `PromptPopupWindow.xaml.cs`:
     - Borderless, topmost, pre-created and hidden at startup for instant (< 200 ms) activation.
     - Modern dark glass styling with target application pill and elevated warning badge.
     - Text input with auto-focus, status line, mic button stub, Send button, and close button.
     - Keyboard navigation: Enter submits, Esc cancels. Controls have `AutomationProperties.Name`.
5. **App Composition Root** (`HolyHand.App/App.xaml.cs`):
   - DI registration for `WindowCaptureService` and `LowLevelKeyboardHook`.
   - Wires hotkey trigger to capture foreground window and show popup.
   - Wires kill switch to cancel and dismiss popup.

### Test Results
- Unit tests in `ChordStateMachineTests.cs` covering HK-01 through HK-07:
  - `HK01_CtrlDown_WinDown_WinUp_FiresExactlyOnce`: Passed.
  - `HK01_WinDown_CtrlDown_CtrlUp_FiresExactlyOnce`: Passed.
  - `HK02_CtrlWinD_InterveningKey_DoesNotFire`: Passed.
  - `HK03_CtrlAlone_Or_WinAlone_DoesNotFire`: Passed.
  - `HK04_LeftAndRightModifierVariants_BothWork`: Passed.
  - `HK05_InjectedEvents_AreIgnored`: Passed.
  - `HK06_TriggerWhileRunActive_EmitsCancelNotShow`: Passed.
  - `HK06_EscapeWhileRunActive_EmitsCancel`: Passed.
  - `HK07_KeyAutoRepeat_DoesNotDoubleFire`: Passed.
- Total tests: **10 passed, 0 failed, 0 skipped**.
- Build status: **0 Warning(s), 0 Error(s)** under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

### Graphify Update
- Ran `graphify update .`:
  - **789 nodes, 1444 edges, 31 communities**.
  - **God nodes**: `AppDelegate` (32), `TaskRunner` (29), `ControllerError` (27), `JevClient` (23), `LowLevelKeyboardHook` (19), `App` (16).
  - Updated `graphify-out/GRAPH_REPORT.md`.

---

## 2026-09-21 — M3: Jev Client via Vercel AI Gateway

### Session Details
- **Date**: 2026-09-21
- **Milestone**: M3 — Jev Client via Vercel AI Gateway

### Components Built
1. **Typed Exceptions & Redaction** (`HolyHand.Core/Jev/`):
   - `JevExceptions.cs`: `JevException`, `AuthException` (401/403), `TransientException` (429/5xx), `ProtocolException`.
   - Automatic API key sanitization: all regex patterns matching `vck_*` and `Bearer *` are replaced with `[REDACTED]` in exception messages.
2. **Configuration & Options** (`HolyHand.Core/Jev/`):
   - `JevOptions.cs`: Options model supporting `BaseUrl`, `ModelId` (`typesafe-ai/jev`), `ApiKey`, `ZeroDataRetention`, `DecisionConfidenceThreshold` (0.70), `RiskConfidenceThreshold`, `TimeoutSeconds`, and `MaxRetries`. Reads from environment / `.env`.
3. **Data Transfer Objects** (`HolyHand.Core/Jev/`):
   - `JevDtos.cs`: `EvaluateRequest`, `QuestionDefinition` (`boolean`, `choice`, `score`), `GatewayProviderOptions`, `EvaluateResponse`, `UsageInfo`, `ProviderMetadataInfo`. Helper methods `TryGetBooleanAnswer`, `TryGetChoiceAnswer`, and `TryGetScoreAnswer`.
4. **Typed Evaluate Client** (`HolyHand.Core/Jev/`):
   - `IJevClient.cs` and `JevClient.cs`:
     - Calls `POST https://ai-gateway.vercel.sh/v1/evaluate`.
     - Resilience: retry with exponential backoff and random jitter on 429/5xx only. No retry on 4xx client errors.
     - Honour cancellation: kill switch / cancellation token aborts in-flight HTTP immediately.
     - Logs latency and cost from `providerMetadata.gateway.cost`.
5. **Decision Model Implementation** (`HolyHand.Core/Jev/`):
   - `JevDecisionModel.cs`: Implements `IDecisionModel`.
     - Dynamically builds deterministic candidate action list (`click:{id}`, `type:{id}:{phrase}`, `press:enter`, `scroll:down`, `wait`, `done`, `ask_user`).
     - Extracts literal/search phrases from goal via regex.
     - Sends Call A with `nextAction` (choice) and `goalAchieved` (boolean).
     - Enforces confidence gate: choices below threshold return `AgentOperation.AskUser`.
     - `VerifyCompletionAsync` sends a strict completion check.
6. **CLI Live Check** (`HolyHand.Cli/Program.cs`):
   - Updated `check` command to perform a live evaluation call to `https://ai-gateway.vercel.sh/v1/evaluate`.
   - Safely prints redacted key (`vck_...XnQc`), measures latency, and handles gateway response.

### Test Results
- Unit test suite `JevClientTests.cs` (JV-01 through JV-08):
  - `JV01_RequestJson_MatchesSchema`: Passed.
  - `JV02_BooleanChoiceScore_AnswersParseCorrectly`: Passed.
  - `JV03_401Unauthorized_ThrowsAuthException_WithoutRetrying`: Passed.
  - `JV04_500InternalError_RetriesWithBackoff_ThenThrowsTransientException`: Passed.
  - `JV05_Cancellation_IsHonouredPromptly`: Passed.
  - `JV06_ApiKey_NeverAppearsInExceptionMessages`: Passed.
  - `JV07_MalformedJson_ThrowsProtocolException`: Passed.
  - `JV08_LowConfidenceDecision_ReturnsAskUser`: Passed.
- Total solution tests: **18 passed, 0 failed**.
- Build status: **0 Warning(s), 0 Error(s)** under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- Live Gateway Check: Reached `https://ai-gateway.vercel.sh/v1/evaluate` with user key.
  - Initial run: Gateway requested card verification for free credits. User verified card on Vercel dashboard.
  - Follow-up run: Gateway reported `Zero Data Retention (ZDR) is only available for Pro and Enterprise plans. Current plan: hobby.`
  - Resolution per AGENTS.md §5.1: Set `ZeroDataRetention = false` by default, made `zeroDataRetention` nullable and omitted when disabled.
  - Serializer hardening: Updated `GatewayMetadata.Routing` to `JsonElement?` (gateway returns nested routing object) and `Cost` to resilient string/number parser with `[JsonExtensionData]` on all DTOs.
  - **Live Verification Result (JV-09)**: ✅ **SUCCESS**!
    ```
    Testing live Jev model via Vercel AI Gateway...
      Gateway Base URL: https://ai-gateway.vercel.sh
      Model: typesafe-ai/jev
      Zero Data Retention: False

    [SUCCESS] Jev evaluation call returned successfully!
      Latency: 843ms
      Answer 'operational': False (probability: 31.0%)
      Tokens: 0 (Prompt: 0, Completion: 0)
      Cost: $0.000000

    All diagnostic checks passed. Your API key and Gateway connection are fully verified.
    ```

### Graphify Update
- Ran `graphify update .`:
  - **867 nodes, 1565 edges, 44 communities**.
  - Updated `graphify-out/GRAPH_REPORT.md`.

### Next Step
- Milestone M4: Screen Reading & UIA3 Snapshot (`FlaUI.UIA3` tree walker, CacheRequest batch reads, node/depth caps, stable IDs, password filtering, and `Cli snapshot`).

---

## 2026-09-21 — M4: Screen Reading & UIA3 Snapshot

### Session Details
- **Date**: 2026-09-21
- **Milestone**: M4 — Screen Reading & UIA3 Snapshot

### Components Built
1. **Screen Reading Configuration & Sanitization** (`HolyHand.Core/ScreenReading/`):
   - `ScreenReaderOptions.cs`: Options model supporting `MaxNodes` (500), `MaxDepth` (30), `MaxCandidates` (40), `OcrFallbackThreshold` (5 interactive controls), `FilterOffscreen` (true), and `FilterDisabled` (false).
   - `SecretSanitizer.cs`: Security boundary redacting password fields to `[PASSWORD]`, credit card numbers (`(?:\d[ -]*?){13,16}`) to `[REDACTED_CARD]`, API keys (`vck_*`, `sk-*`, `ghp_*`) to `[REDACTED_KEY]`, and Bearer tokens to `Bearer [REDACTED]`.
   - `ElementRanker.cs`: Deterministic stable sequential ID assignment (`e1`, `e2`...), priority ranking (focused first, interactive controls second, labeled controls third, outcome evidence fourth, visual top-left ordering fifth).
2. **UIA3 Screen Reader** (`HolyHand.Platform/ScreenReading/`):
   - `UiaScreenReader.cs`: Implements `IScreenReader`.
   - Attaches to target HWND via `UIA3Automation.FromHandle`.
   - Uses `CacheRequest` with `TreeScope.Subtree` to pre-fetch `Name`, `ControlType`, `IsEnabled`, `BoundingRectangle`, `IsKeyboardFocusable`, `HasKeyboardFocus`, `IsPassword`, and `IsOffscreen` in a single cross-process roundtrip.
   - UIPI elevation check: refuses elevated administrator processes with a clear error message.
   - Activates local OCR fallback if interactive element count is below threshold.
3. **Local On-Device OCR** (`HolyHand.Platform/ScreenReading/`):
   - `WindowsOcrService.cs`: Uses built-in `Windows.Media.Ocr.OcrEngine` and `Windows.Graphics.Imaging.SoftwareBitmap`. Captures bitmap via `Graphics.CopyFromScreen` in memory with zero external dependencies and zero cloud egress.
4. **Targeted Window Capture** (`HolyHand.Platform/Windowing/`):
   - Refactored `WindowCaptureService.cs` with `CaptureWindowByProcessName` and `CaptureWindowByProcessId` via `EnumWindows` and `PInvoke.GetWindowRect`.
5. **CLI Snapshot Subcommand** (`HolyHand.Cli/Program.cs`):
   - `HolyHand.Cli snapshot [processName|PID]`: captures targeted window or foreground window, runs `UiaScreenReader`, measures elapsed milliseconds, and outputs a formatted table of interactive and OCR elements.

### Test Results
- Unit and UI test suite (`ScreenReaderTests.cs`):
  - **RD-01**: Real window live snapshot contains controls with role, label, value, and password redacted (100% passing).
  - **RD-02**: Node cap (500) and candidate cap (40) enforced; IDs and order 100% stable across identical runs (100% passing).
  - **RD-03**: Password fields and secret tokens (credit cards, API keys, bearer headers) strictly redacted (100% passing).
  - **RD-04**: Offscreen elements with zero size filtered; disabled elements flagged/filtered (100% passing).
  - **RD-06**: Windows OCR on known test bitmap returns text and bounding boxes (100% passing).
  - **RD-07**: Elevated target processes detected and refused with clear message (100% passing).
- Total solution tests: **24 passed, 0 failed, 0 skipped**.
- Build status: **0 Warning(s), 0 Error(s)** under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

### Graphify Update
- Updated graph with `ScreenReaderOptions`, `SecretSanitizer`, `ElementRanker`, `WindowsOcrService`, and `UiaScreenReader`.

### Next Step
- Milestone M5: Agent Loop in Dry-Run (`observe → build candidates → decide (Jev) → risk check → verify → repeat`, loop guard, max steps, action executor with UIA patterns and SendInput fallback, dry-run by default).

---

## 2026-09-21 — M5: Agent Loop in Dry-Run

### Session Details
- **Date**: 2026-09-21
- **Milestone**: M5 — Agent Loop in Dry-Run

### Components Built
1. **Agent Loop Configuration & Guards** (`HolyHand.Core/Agent/`):
   - `AgentLoopOptions.cs`: Options model supporting `DryRun` (default: true), `MaxSteps` (default: 15), `MaxConsecutiveStalls` (default: 3), and `ActionTimeoutSeconds` (default: 10).
   - `LoopGuard.cs`: Pure stall detector computing stable SHA-256 state signatures across element lists and detecting repetitive unchanged states to prevent infinite loops.
   - `UrlLauncherValidator.cs`: Strict URL security filter allowing only `http` and `https` schemes derived from the user goal, rejecting dangerous URI schemes (`file:`, `javascript:`, `cmd:`).
2. **Agent Loop Orchestrator** (`HolyHand.Core/Agent/`):
   - `AgentLoop.cs`: Pure orchestrator executing the full feedback cycle:
     - `observe`: reads element snapshot via `IScreenReader`.
     - `decide`: calls `IDecisionModel.DecideNextActionAsync` with ranked candidates and user goal.
     - `act`: delegates to `IActionExecutor` (respecting `DryRun` flag).
     - `verify`: re-reads screen state to compute state diff and calls `IDecisionModel.VerifyCompletionAsync`.
     - `loop guard & limits`: stops on max steps exceeded, consecutive stalls, goal achieved, human consultation requested, or kill switch cancellation.
     - Emits `StatusChanged` and `StepCompleted` events for UI status and live diagnostics.
3. **Win32 Input Simulator** (`HolyHand.Platform/Execution/`):
   - `InputSimulator.cs`: Low-level wrapper around CsWin32 `SendInput`:
     - Mouse clicking via `MOUSEEVENTF_LEFTDOWN` / `MOUSEEVENTF_LEFTUP`.
     - Mouse wheel scrolling via `MOUSEEVENTF_WHEEL`.
     - Keyboard key down/up with `VIRTUAL_KEY` (Return, Tab, Escape).
     - Unicode text typing via `KEYEVENTF_UNICODE`.
4. **Action Executor with Pattern Priority & Process Guard** (`HolyHand.Platform/Execution/`):
   - `ActionExecutor.cs`: Implements `IActionExecutor`.
     - Respects `DryRun` mode (simulates actions with logging and 150ms pacing).
     - Live execution: checks foreground window process ID before every action (`GetForegroundProcessId()`) to immediately abort if the user changed apps mid-run (ProcessGuard).
     - Pattern traversal: searches deepest element from point and walks up ancestor chain for `InvokePattern`, `TogglePattern`, `SelectionItemPattern`, and `ValuePattern` before falling back to `SendInput`.
5. **CLI Dry-Run Subcommand** (`HolyHand.Cli/Program.cs`):
   - `HolyHand.Cli dry-run <goal>`: captures foreground target window, spins up `UiaScreenReader`, `JevDecisionModel`, `ActionExecutor`, and `AgentLoop` in dry-run mode, logs step-by-step decisions with confidence, and handles Ctrl+C kill switch cleanly.

### Test Results
- Unit and UI test suite (`AgentLoopTests.cs` and `ActionExecutorTests.cs`):
  - **EX-01**: ValuePattern text entry into WPF window verified with live read-back (Passed).
  - **EX-02**: InvokePattern button click verified on live WPF window (Passed).
  - **EX-03**: SendInput fallback used when control lacks pattern support (Passed).
  - **EX-04**: ProcessGuard aborts execution if foreground process changes mid-action (Passed).
  - **EX-05**: LoopGuard halts execution upon reaching consecutive unchanged state limit (Passed).
  - **EX-06**: Max steps cap stops execution with `MaxStepsReached` status (Passed).
  - **EX-07**: URL validator accepts valid http/https URLs and rejects unsafe schemes (Passed).
- Sequential UI test isolation: Added `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to prevent multi-threaded WPF desktop focus collisions in xUnit.
- Total solution tests: **31 passed, 0 failed, 0 skipped** across all test suites.
- Build status: **0 Warning(s), 0 Error(s)** under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

### Graphify Update
- Ran `graphify update .`:
  - Updated graph with `AgentLoop`, `AgentLoopOptions`, `LoopGuard`, `UrlLauncherValidator`, `InputSimulator`, and `ActionExecutor`.
  - Updated `graphify-out/GRAPH_REPORT.md`.

### Next Step
- Milestone M6: Safety and Real Execution (`IRiskPolicy`, confirmation dialog, kill switch, deny-list, audit log in `%LOCALAPPDATA%\HolyHand\audit`, Jev risk-escalation call, enable real execution toggle).







