# Graph Report - third-hand  (2026-09-21)

## Corpus Check
- 57 files · ~27,767 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 676 nodes · 1247 edges · 30 communities (26 shown, 4 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 104 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `29b87f98`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- HolyHand.App.csproj
- App
- CoreInterfaces.cs
- .runLoop
- AppDelegate
- AgentDecision
- ApplicationServices
- ScaffoldTests.cs
- .frontWindow
- InputController
- .prepare
- Foundation
- PROMPT START
- 1. Upstream Source Files
- .run
- CDPClient
- 2026-09-21 — M0: Recon and Setup
- HotkeyManager
- XCTestCase
- AccessibilityElement
- AppTarget
- Program
- HolyHand — Architecture Decision Records
- Third Hand
- HolyHandApp
- HolyHand — Agent Instructions
- HolyHand — Port Status
- rebuild.sh script
- Package.swift
- notarize.sh

## God Nodes (most connected - your core abstractions)
1. `AppDelegate` - 32 edges
2. `TaskRunner` - 29 edges
3. `ControllerError` - 27 edges
4. `JevClient` - 23 edges
5. `AgentDecision` - 20 edges
6. `JevTests` - 19 edges
7. `CDPClient` - 16 edges
8. `RunProgressTests` - 15 edges
9. `ControllerTests` - 14 edges
10. `OverlayPanel` - 13 edges

## Surprising Connections (you probably didn't know these)
- `rebuild.sh script` --calls--> `build.sh script`  [EXTRACTED]
  rebuild.sh → Scripts/build.sh
- `TaskRunner` --calls--> `RunProgress`  [INFERRED]
  Sources/HolyHand/TaskRunner.swift → Sources/HolyHand/RunProgress.swift
- `AppDelegate` --references--> `HotkeyManager`  [EXTRACTED]
  Sources/HolyHand/AppDelegate.swift → Sources/HolyHand/HotkeyManager.swift
- `AppDelegate` --references--> `TaskRunner`  [EXTRACTED]
  Sources/HolyHand/AppDelegate.swift → Sources/HolyHand/TaskRunner.swift
- `AppDelegate` --implements--> `TaskRunnerDelegate`  [EXTRACTED]
  Sources/HolyHand/AppDelegate.swift → Sources/HolyHand/TaskRunner.swift

## Import Cycles
- None detected.

## Communities (30 total, 4 thin omitted)

### Community 0 - "HolyHand.App.csproj"
Cohesion: 0.07
Nodes (27): coverlet.collector, FlaUI.UIA3, FluentAssertions, H.NotifyIcon.Wpf, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Test.Sdk, Microsoft.Windows.CsWin32, Moq (+19 more)

### Community 1 - "App"
Cohesion: 0.15
Nodes (9): HolyHand.App, ExitEventArgs, IServiceCollection, Mutex, ServiceProvider, StartupEventArgs, Application, string (+1 more)

### Community 2 - "CoreInterfaces.cs"
Cohesion: 0.06
Nodes (33): HolyHand.Core.Common, HolyHand.Core.Interfaces, HolyHand.Core.Models, IDisposable, IntPtr, CancellationToken, DateTimeOffset, Task (+25 more)

### Community 3 - ".runLoop"
Cohesion: 0.06
Nodes (45): AnyObject, Int32, LocalizedError, ActionHistory, ControllerError, .errorDescription, String, AccessibilityElement (+37 more)

### Community 4 - "AppDelegate"
Cohesion: 0.06
Nodes (41): AppKit, CGFloat, Notification, NSApplication, NSApplicationDelegate, NSHostingView, NSObject, NSPanel (+33 more)

### Community 5 - "AgentDecision"
Cohesion: 0.11
Nodes (20): Codable, Equatable, AgentDecision, Double, ActionVerification, ObservationState, RunProgress, AccessibilityElement (+12 more)

### Community 6 - "ApplicationServices"
Cohesion: 0.08
Nodes (12): ApplicationServices, CompletionProtocol, RejectedRequestProtocol, Bool, URLRequest, TextOnlyProtocol, Bool, URLRequest (+4 more)

### Community 7 - "ScaffoldTests.cs"
Cohesion: 0.40
Nodes (3): HolyHand.Tests, Fact, ScaffoldTests

### Community 8 - ".frontWindow"
Cohesion: 0.09
Nodes (18): ScreenCaptureKit, AccessibilityElement, CGImage, CGRect, pid_t, VisionObserver, CGImage, CGPoint (+10 more)

### Community 9 - "InputController"
Cohesion: 0.08
Nodes (21): CGKeyCode, Character, Date, invalid, AccessibilityElement, Bool, AXTreeWalker, AccessibilityElement (+13 more)

### Community 10 - ".prepare"
Cohesion: 0.12
Nodes (13): Node, Failure, changed, .errorDescription, unavailable, AXUIElement, Bool, Int (+5 more)

### Community 11 - "Foundation"
Cohesion: 0.12
Nodes (12): Foundation, Security, ElectronDetector, AppTarget, Bool, Int, pid_t, String (+4 more)

### Community 12 - "PROMPT START"
Cohesion: 0.10
Nodes (20): 10. Test plan (write these; unit tests must need neither network nor a UI session), 11. Performance targets and demos, 12. Your first actions now, 1. Mission, 2. Ground rules, 3. Target stack (chosen for speed and small footprint on user PCs), 4. Architecture, 5.1 Use the plain HTTP endpoint (no TypeScript / AI SDK needed) (+12 more)

### Community 13 - "1. Upstream Source Files"
Cohesion: 0.10
Nodes (19): 1.1 App Shell & Lifecycle, 1.2 Hotkey & Input, 1.3 UI, 1.4 Screen Reading, 1.5 Agent Loop & Decision, 1.6 Browser Support (M9), 1.7 Build Scripts (Drop), 1. Upstream Source Files (+11 more)

### Community 14 - ".run"
Cohesion: 0.15
Nodes (13): CheckedContinuation, Error, MainActor, Result, AsyncTimeout, Race, Never, String (+5 more)

### Community 15 - "CDPClient"
Cohesion: 0.24
Nodes (14): Decodable, CDPClient, .isConnected, DOMSnapshot, Element, Rect, Screen, Any (+6 more)

### Community 16 - "2026-09-21 — M0: Recon and Setup"
Cohesion: 0.12
Nodes (16): 2026-09-21 — M0: Recon and Setup, 2026-09-21 — M1: Scaffold, Files Created, Files Read (upstream), Graphify, Graphify Update, Key Findings, Known Issues & Next Steps (+8 more)

### Community 17 - "HotkeyManager"
Cohesion: 0.14
Nodes (13): CFMachPort, CFRunLoopSource, CGEventTapProxy, CGEventType, CoreGraphics, hotkeyEventCallback(), HotkeyManager, .isRunning (+5 more)

### Community 18 - "XCTestCase"
Cohesion: 0.18
Nodes (7): Bool, String, TextEntryPlan, String, TextExtractor, TextEntryPlanTests, XCTestCase

### Community 19 - "AccessibilityElement"
Cohesion: 0.22
Nodes (9): AccessibilityElement, .displayLabel, .displayRole, .isOutcomeEvidence, AXUIElement, Bool, CGRect, Int (+1 more)

### Community 20 - "AppTarget"
Cohesion: 0.29
Nodes (6): NSRunningApplication, AppTarget, AXUIElement, NSImage, pid_t, String

### Community 22 - "HolyHand — Architecture Decision Records"
Cohesion: 0.22
Nodes (8): ADR-001: .NET 8 LTS over .NET 9, ADR-002: Vercel AI Gateway vs Direct TypeSafe API, ADR-003: Ctrl+Win Chord via Low-Level Hook + State Machine, ADR-004: FlaUI.UIA3 for UI Automation, ADR-005: CsWin32 Source Generator for P/Invoke, ADR-006: Serilog with File Sink for Logging, ADR-007: Whisper.net for Local Voice Input, HolyHand — Architecture Decision Records

### Community 23 - "Third Hand"
Cohesion: 0.25
Nodes (7): Build from source, Development, Download, How it works, License, Status, Third Hand

### Community 24 - "HolyHandApp"
Cohesion: 0.40
Nodes (4): App, Scene, HolyHandApp, .body

### Community 25 - "HolyHand — Agent Instructions"
Cohesion: 0.40
Nodes (4): 2. Ground rules, 7. Safety invariants (non-negotiable), graphify, HolyHand — Agent Instructions

### Community 26 - "HolyHand — Port Status"
Cohesion: 0.50
Nodes (3): New Windows Capabilities, HolyHand — Port Status, Upstream Capabilities

## Knowledge Gaps
- **115 isolated node(s):** `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence`, `.displayRole`, `.displayLabel` (+110 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TaskRunner` connect `.runLoop` to `AppDelegate`, `AgentDecision`, `CDPClient`?**
  _High betweenness centrality (0.073) - this node is a cross-community bridge._
- **Why does `AppDelegate` connect `AppDelegate` to `HotkeyManager`, `.runLoop`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **Why does `ControllerError` connect `.runLoop` to `.frontWindow`, `InputController`, `Foundation`, `.run`, `CDPClient`, `XCTestCase`?**
  _High betweenness centrality (0.065) - this node is a cross-community bridge._
- **Are the 9 inferred relationships involving `JevClient` (e.g. with `.runLoop()` and `.startLoading()`) actually correct?**
  _`JevClient` has 9 INFERRED edges - model-reasoned connections that need verification._
- **Are the 14 inferred relationships involving `AgentDecision` (e.g. with `.testOCRLabelsCannotBecomeClickTargetsWithoutVisualGrounding()` and `.testOCRTextUsesExplicitClickTextRatherThanPretendingToBeAButton()`) actually correct?**
  _`AgentDecision` has 14 INFERRED edges - model-reasoned connections that need verification._
- **What connects `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence` to the rest of the system?**
  _115 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `HolyHand.App.csproj` be split into smaller, more focused modules?**
  _Cohesion score 0.06722689075630252 - nodes in this community are weakly interconnected._