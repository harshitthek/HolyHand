# Graph Report - third-hand  (2026-09-21)

## Corpus Check
- 67 files · ~30,265 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 789 nodes · 1444 edges · 31 communities (28 shown, 3 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 115 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `84fc7791`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- HolyHand.App.csproj
- App
- CoreInterfaces.cs
- JevClient
- AppDelegate
- AgentDecision
- JevTests.swift
- ScaffoldTests.cs
- .frontWindow
- AXTreeWalker
- .prepare
- Foundation
- PROMPT START
- 1. Upstream Source Files
- .run
- .runLoop
- 2026-09-21 — M0: Recon and Setup
- ChordStateMachine
- PromptPopupWindow
- AccessibilityElement
- AppKit
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

## God Nodes (most connected - your core abstractions)
1. `AppDelegate` - 32 edges
2. `TaskRunner` - 29 edges
3. `ControllerError` - 27 edges
4. `JevClient` - 23 edges
5. `AgentDecision` - 20 edges
6. `JevTests` - 19 edges
7. `LowLevelKeyboardHook` - 19 edges
8. `CDPClient` - 16 edges
9. `App` - 16 edges
10. `RunProgressTests` - 15 edges

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

## Communities (31 total, 3 thin omitted)

### Community 0 - "HolyHand.App.csproj"
Cohesion: 0.07
Nodes (27): coverlet.collector, FlaUI.UIA3, FluentAssertions, H.NotifyIcon.Wpf, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Test.Sdk, Microsoft.Windows.CsWin32, Moq (+19 more)

### Community 1 - "App"
Cohesion: 0.05
Nodes (24): HolyHand.Core.Common, HolyHand.App, HolyHand.Platform.Hotkey, HolyHand.Cli, HolyHand.App.Windows, HolyHand.Core.Interfaces, EventArgs, ExitEventArgs (+16 more)

### Community 2 - "CoreInterfaces.cs"
Cohesion: 0.06
Nodes (29): HolyHand.Platform.Windowing, HolyHand.Core.Models, IDisposable, IntPtr, CancellationToken, DateTimeOffset, IReadOnlyList, Task (+21 more)

### Community 3 - "JevClient"
Cohesion: 0.11
Nodes (19): ActionHistory, JevClient, JevResult, JevServiceError, .errorDescription, AccessibilityElement, AgentDecision, Any (+11 more)

### Community 4 - "AppDelegate"
Cohesion: 0.06
Nodes (42): CFMachPort, CFRunLoopSource, Notification, NSApplication, NSApplicationDelegate, NSHostingView, NSObject, NSPanel (+34 more)

### Community 5 - "AgentDecision"
Cohesion: 0.10
Nodes (21): Codable, Equatable, AgentDecision, Double, String, ActionVerification, ObservationState, RunProgress (+13 more)

### Community 6 - "JevTests.swift"
Cohesion: 0.07
Nodes (15): Bool, String, TextEntryPlan, CompletionProtocol, RejectedRequestProtocol, Bool, URLRequest, TextOnlyProtocol (+7 more)

### Community 7 - "ScaffoldTests.cs"
Cohesion: 0.40
Nodes (3): HolyHand.Tests, Fact, ScaffoldTests

### Community 8 - ".frontWindow"
Cohesion: 0.10
Nodes (17): AccessibilityElement, CGImage, CGRect, pid_t, VisionObserver, CGImage, CGPoint, CGRect (+9 more)

### Community 9 - "AXTreeWalker"
Cohesion: 0.17
Nodes (11): Date, AXTreeWalker, AccessibilityElement, AnyObject, AppTarget, AXUIElement, Bool, Int (+3 more)

### Community 10 - ".prepare"
Cohesion: 0.12
Nodes (14): LocalizedError, Node, Failure, changed, .errorDescription, unavailable, AXUIElement, Bool (+6 more)

### Community 11 - "Foundation"
Cohesion: 0.09
Nodes (14): Foundation, Security, ElectronDetector, AppTarget, Bool, Int, pid_t, String (+6 more)

### Community 12 - "PROMPT START"
Cohesion: 0.10
Nodes (20): 10. Test plan (write these; unit tests must need neither network nor a UI session), 11. Performance targets and demos, 12. Your first actions now, 1. Mission, 2. Ground rules, 3. Target stack (chosen for speed and small footprint on user PCs), 4. Architecture, 5.1 Use the plain HTTP endpoint (no TypeScript / AI SDK needed) (+12 more)

### Community 13 - "1. Upstream Source Files"
Cohesion: 0.10
Nodes (19): 1.1 App Shell & Lifecycle, 1.2 Hotkey & Input, 1.3 UI, 1.4 Screen Reading, 1.5 Agent Loop & Decision, 1.6 Browser Support (M9), 1.7 Build Scripts (Drop), 1. Upstream Source Files (+11 more)

### Community 14 - ".run"
Cohesion: 0.14
Nodes (14): CheckedContinuation, Error, MainActor, Result, AsyncTimeout, Race, Never, String (+6 more)

### Community 15 - ".runLoop"
Cohesion: 0.06
Nodes (47): AnyObject, CGKeyCode, Character, Decodable, Int32, ControllerError, .errorDescription, invalid (+39 more)

### Community 16 - "2026-09-21 — M0: Recon and Setup"
Cohesion: 0.11
Nodes (17): 2026-09-21 — M0: Recon and Setup, 2026-09-21 — M1: Scaffold, Files Created, Files Read (upstream), Graphify, Graphify Update, Key Findings, Known Issues & Next Steps (+9 more)

### Community 17 - "ChordStateMachine"
Cohesion: 0.14
Nodes (14): ChordArmed, HolyHand.Tests.Hotkey, HolyHand.Core.Hotkey, CtrlDown, Interrupted, WinDown, bool, int (+6 more)

### Community 18 - "PromptPopupWindow"
Cohesion: 0.13
Nodes (17): KeyEventArgs, RoutedEventArgs, TextChangedEventArgs, CloseButton, ElevatedBadge, MicButton, PlaceholderText, PromptInput (+9 more)

### Community 19 - "AccessibilityElement"
Cohesion: 0.22
Nodes (9): AccessibilityElement, .displayLabel, .displayRole, .isOutcomeEvidence, AXUIElement, Bool, CGRect, Int (+1 more)

### Community 20 - "AppKit"
Cohesion: 0.06
Nodes (25): App, AppKit, ApplicationServices, CGEventTapProxy, CGEventType, CGFloat, CoreGraphics, NSRunningApplication (+17 more)

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

## Knowledge Gaps
- **124 isolated node(s):** `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence`, `.displayRole`, `.displayLabel` (+119 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TaskRunner` connect `.runLoop` to `JevClient`, `AppDelegate`, `AgentDecision`?**
  _High betweenness centrality (0.053) - this node is a cross-community bridge._
- **Why does `AppDelegate` connect `AppDelegate` to `AppKit`, `.runLoop`?**
  _High betweenness centrality (0.051) - this node is a cross-community bridge._
- **Why does `ControllerError` connect `.runLoop` to `JevClient`, `AgentDecision`, `JevTests.swift`, `.frontWindow`, `.prepare`, `Foundation`, `.run`?**
  _High betweenness centrality (0.048) - this node is a cross-community bridge._
- **Are the 9 inferred relationships involving `JevClient` (e.g. with `.runLoop()` and `.startLoading()`) actually correct?**
  _`JevClient` has 9 INFERRED edges - model-reasoned connections that need verification._
- **Are the 14 inferred relationships involving `AgentDecision` (e.g. with `.testOCRLabelsCannotBecomeClickTargetsWithoutVisualGrounding()` and `.testOCRTextUsesExplicitClickTextRatherThanPretendingToBeAButton()`) actually correct?**
  _`AgentDecision` has 14 INFERRED edges - model-reasoned connections that need verification._
- **What connects `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence` to the rest of the system?**
  _124 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `HolyHand.App.csproj` be split into smaller, more focused modules?**
  _Cohesion score 0.06722689075630252 - nodes in this community are weakly interconnected._