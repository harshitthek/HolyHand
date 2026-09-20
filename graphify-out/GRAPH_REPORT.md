# Graph Report - HolyHand  (2026-09-21)

## Corpus Check
- 80 files · ~37,603 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 936 nodes · 1706 edges · 41 communities (36 shown, 5 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 117 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `0f326824`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- HolyHand.App.csproj
- App
- AccessibilityElement
- JevClient
- .ReadElementsAsync
- AgentDecision
- JevTests.swift
- ScaffoldTests.cs
- AccessibilityElement
- AXTreeWalker
- .prepare
- KeychainHelper
- PROMPT START
- 1. Upstream Source Files
- .run
- TaskRunner
- 2026-09-21 — M0: Recon and Setup
- ChordStateMachine
- PromptPopupWindow
- JevDecisionModel
- ElectronDetector
- LowLevelKeyboardHook
- HolyHand — Architecture Decision Records
- Third Hand
- windows/NativeMethods.json
- HolyHand — Agent Instructions
- HolyHand — Port Status
- rebuild.sh script
- Package.swift
- notarize.sh
- HolyHand.Platform/NativeMethods.json
- JevTests
- AppDelegate
- .RecognizeBitmapAsync
- SystemClock
- .Main
- HolyHand.Core.Models
- UiaScreenReader
- AppTarget
- .frontWindow
- ActionResult

## God Nodes (most connected - your core abstractions)
1. `AppDelegate` - 32 edges
2. `TaskRunner` - 29 edges
3. `ControllerError` - 27 edges
4. `AgentDecision` - 20 edges
5. `JevClient` - 19 edges
6. `JevTests` - 19 edges
7. `AccessibilityElement` - 19 edges
8. `LowLevelKeyboardHook` - 19 edges
9. `AppTarget` - 18 edges
10. `CDPClient` - 16 edges

## Surprising Connections (you probably didn't know these)
- `rebuild.sh script` --calls--> `build.sh script`  [EXTRACTED]
  rebuild.sh → Scripts/build.sh
- `TaskRunner` --calls--> `RunProgress`  [INFERRED]
  Sources/HolyHand/TaskRunner.swift → Sources/HolyHand/RunProgress.swift
- `TaskRunner` --references--> `ActionHistory`  [EXTRACTED]
  Sources/HolyHand/TaskRunner.swift → Sources/HolyHand/AgentTypes.swift
- `AppDelegate` --references--> `TaskRunner`  [EXTRACTED]
  Sources/HolyHand/AppDelegate.swift → Sources/HolyHand/TaskRunner.swift
- `AppDelegate` --implements--> `TaskRunnerDelegate`  [EXTRACTED]
  Sources/HolyHand/AppDelegate.swift → Sources/HolyHand/TaskRunner.swift

## Import Cycles
- None detected.

## Communities (41 total, 5 thin omitted)

### Community 0 - "HolyHand.App.csproj"
Cohesion: 0.07
Nodes (27): coverlet.collector, FlaUI.UIA3, FluentAssertions, H.NotifyIcon.Wpf, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Test.Sdk, Microsoft.Windows.CsWin32, Moq (+19 more)

### Community 1 - "App"
Cohesion: 0.09
Nodes (12): EventArgs, ExitEventArgs, IServiceCollection, Mutex, ServiceProvider, StartupEventArgs, Application, AppTarget (+4 more)

### Community 2 - "AccessibilityElement"
Cohesion: 0.11
Nodes (16): CancellationToken, IReadOnlyList, Task, TimeSpan, IActionExecutor, IAuditLog, ICredentialStore, IDecisionModel (+8 more)

### Community 3 - "JevClient"
Cohesion: 0.09
Nodes (23): Foundation, ActionHistory, JevClient, JevResult, JevServiceError, .errorDescription, AccessibilityElement, AgentDecision (+15 more)

### Community 4 - ".ReadElementsAsync"
Cohesion: 0.12
Nodes (13): AutomationElement, HashSet, IEnumerable, Regex, IReadOnlyList, ElementRanker, SecretSanitizer, CancellationToken (+5 more)

### Community 5 - "AgentDecision"
Cohesion: 0.10
Nodes (21): Codable, Equatable, AgentDecision, Double, String, ActionVerification, ObservationState, RunProgress (+13 more)

### Community 6 - "JevTests.swift"
Cohesion: 0.09
Nodes (11): CompletionProtocol, RejectedRequestProtocol, Bool, URLRequest, TextOnlyProtocol, Bool, URLRequest, TextSelectionProtocol (+3 more)

### Community 7 - "ScaffoldTests.cs"
Cohesion: 0.40
Nodes (3): HolyHand.Tests, Fact, ScaffoldTests

### Community 8 - "AccessibilityElement"
Cohesion: 0.05
Nodes (30): App, AppKit, ApplicationServices, CGEventTapProxy, CGEventType, CoreGraphics, NSRunningApplication, Scene (+22 more)

### Community 9 - "AXTreeWalker"
Cohesion: 0.17
Nodes (11): Date, AXTreeWalker, AccessibilityElement, AnyObject, AppTarget, AXUIElement, Bool, Int (+3 more)

### Community 10 - ".prepare"
Cohesion: 0.12
Nodes (14): LocalizedError, Node, Failure, changed, .errorDescription, unavailable, AXUIElement, Bool (+6 more)

### Community 11 - "KeychainHelper"
Cohesion: 0.36
Nodes (4): Security, KeychainHelper, Any, String

### Community 12 - "PROMPT START"
Cohesion: 0.10
Nodes (20): 10. Test plan (write these; unit tests must need neither network nor a UI session), 11. Performance targets and demos, 12. Your first actions now, 1. Mission, 2. Ground rules, 3. Target stack (chosen for speed and small footprint on user PCs), 4. Architecture, 5.1 Use the plain HTTP endpoint (no TypeScript / AI SDK needed) (+12 more)

### Community 13 - "1. Upstream Source Files"
Cohesion: 0.10
Nodes (19): 1.1 App Shell & Lifecycle, 1.2 Hotkey & Input, 1.3 UI, 1.4 Screen Reading, 1.5 Agent Loop & Decision, 1.6 Browser Support (M9), 1.7 Build Scripts (Drop), 1. Upstream Source Files (+11 more)

### Community 14 - ".run"
Cohesion: 0.14
Nodes (14): CheckedContinuation, Error, MainActor, Result, AsyncTimeout, Race, Never, String (+6 more)

### Community 15 - "TaskRunner"
Cohesion: 0.06
Nodes (48): AnyObject, CGKeyCode, Character, Decodable, Int32, ControllerError, .errorDescription, invalid (+40 more)

### Community 16 - "2026-09-21 — M0: Recon and Setup"
Cohesion: 0.06
Nodes (33): 2026-09-21 — M0: Recon and Setup, 2026-09-21 — M1: Scaffold, 2026-09-21 — M2: Hotkey and Popup, 2026-09-21 — M3: Jev Client via Vercel AI Gateway, 2026-09-21 — M4: Screen Reading & UIA3 Snapshot, Components Built, Components Built, Components Built (+25 more)

### Community 17 - "ChordStateMachine"
Cohesion: 0.14
Nodes (14): ChordArmed, HolyHand.Tests.Hotkey, HolyHand.Core.Hotkey, CtrlDown, Interrupted, WinDown, bool, int (+6 more)

### Community 18 - "PromptPopupWindow"
Cohesion: 0.13
Nodes (17): KeyEventArgs, RoutedEventArgs, TextChangedEventArgs, CloseButton, ElevatedBadge, MicButton, PlaceholderText, PromptInput (+9 more)

### Community 19 - "JevDecisionModel"
Cohesion: 0.05
Nodes (38): HolyHand.Tests.Jev, HolyHand.Core.Jev, Exception, HttpClient, JsonElement, JsonSerializerOptions, List, CancellationToken (+30 more)

### Community 20 - "ElectronDetector"
Cohesion: 0.31
Nodes (6): ElectronDetector, AppTarget, Bool, Int, pid_t, String

### Community 21 - "LowLevelKeyboardHook"
Cohesion: 0.10
Nodes (16): CancellationTokenSource, Channel, DllImport, HHOOK, HOOKPROC, LPARAM, LRESULT, TaskCompletionSource (+8 more)

### Community 22 - "HolyHand — Architecture Decision Records"
Cohesion: 0.22
Nodes (8): ADR-001: .NET 8 LTS over .NET 9, ADR-002: Vercel AI Gateway vs Direct TypeSafe API, ADR-003: Ctrl+Win Chord via Low-Level Hook + State Machine, ADR-004: FlaUI.UIA3 for UI Automation, ADR-005: CsWin32 Source Generator for P/Invoke, ADR-006: Serilog with File Sink for Logging, ADR-007: Whisper.net for Local Voice Input, HolyHand — Architecture Decision Records

### Community 23 - "Third Hand"
Cohesion: 0.25
Nodes (7): Build from source, Development, Download, How it works, License, Status, Third Hand

### Community 24 - "windows/NativeMethods.json"
Cohesion: 0.50
Nodes (3): emitSingleFile, public, $schema

### Community 25 - "HolyHand — Agent Instructions"
Cohesion: 0.40
Nodes (4): 2. Ground rules, 7. Safety invariants (non-negotiable), graphify, HolyHand — Agent Instructions

### Community 26 - "HolyHand — Port Status"
Cohesion: 0.50
Nodes (3): New Windows Capabilities, HolyHand — Port Status, Upstream Capabilities

### Community 30 - "HolyHand.Platform/NativeMethods.json"
Cohesion: 0.50
Nodes (3): emitSingleFile, public, $schema

### Community 31 - "JevTests"
Cohesion: 0.16
Nodes (4): JevTests, AccessibilityElement, Int, String

### Community 32 - "AppDelegate"
Cohesion: 0.05
Nodes (45): CFMachPort, CFRunLoopSource, CGFloat, Notification, NSApplication, NSApplicationDelegate, NSHostingView, NSObject (+37 more)

### Community 33 - ".RecognizeBitmapAsync"
Cohesion: 0.23
Nodes (9): Bitmap, OcrEngine, Point, CancellationToken, ILogger, IReadOnlyList, Rectangle, Task (+1 more)

### Community 34 - "SystemClock"
Cohesion: 0.25
Nodes (7): CancellationToken, DateTimeOffset, Task, TimeSpan, SystemClock, DateTimeOffset, IClock

### Community 36 - "HolyHand.Core.Models"
Cohesion: 0.15
Nodes (11): HolyHand.Platform.ScreenReading, HolyHand.Core.Common, HolyHand.App, HolyHand.Core.ScreenReading, HolyHand.Platform.Hotkey, HolyHand.Platform.Windowing, HolyHand.Cli, HolyHand.App.Windows (+3 more)

### Community 37 - "UiaScreenReader"
Cohesion: 0.20
Nodes (7): IDisposable, UIA3Automation, IHotkeyService, ScreenReaderOptions, bool, ILogger, UiaScreenReader

### Community 39 - "AppTarget"
Cohesion: 0.27
Nodes (6): HWND, IntPtr, Rectangle, AppTarget, IWindowCaptureService, WindowCaptureService

### Community 40 - ".frontWindow"
Cohesion: 0.09
Nodes (17): AccessibilityElement, CGImage, CGRect, pid_t, VisionObserver, CGImage, CGPoint, CGRect (+9 more)

## Knowledge Gaps
- **139 isolated node(s):** `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence`, `.displayRole`, `.displayLabel` (+134 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TaskRunner` connect `TaskRunner` to `AppDelegate`, `JevClient`, `AgentDecision`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **Why does `AppDelegate` connect `AppDelegate` to `AccessibilityElement`, `TaskRunner`?**
  _High betweenness centrality (0.037) - this node is a cross-community bridge._
- **Why does `ControllerError` connect `TaskRunner` to `JevClient`, `AgentDecision`, `.frontWindow`, `.prepare`, `KeychainHelper`, `.run`?**
  _High betweenness centrality (0.034) - this node is a cross-community bridge._
- **Are the 14 inferred relationships involving `AgentDecision` (e.g. with `.testOCRLabelsCannotBecomeClickTargetsWithoutVisualGrounding()` and `.testOCRTextUsesExplicitClickTextRatherThanPretendingToBeAButton()`) actually correct?**
  _`AgentDecision` has 14 INFERRED edges - model-reasoned connections that need verification._
- **Are the 5 inferred relationships involving `JevClient` (e.g. with `.runLoop()` and `.testCompletionCheckDoesNotAskForAnotherAction()`) actually correct?**
  _`JevClient` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence` to the rest of the system?**
  _139 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `HolyHand.App.csproj` be split into smaller, more focused modules?**
  _Cohesion score 0.06890756302521009 - nodes in this community are weakly interconnected._