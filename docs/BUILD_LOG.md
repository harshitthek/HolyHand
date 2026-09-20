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

### Next Step
- Milestone M2: Core Logic (pure C# port of RunProgress, TextEntryPlan, TextExtractor, TextFieldFocus, and ChordStateMachine under `HolyHand.Core`).



