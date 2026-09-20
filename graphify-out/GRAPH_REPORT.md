# Graph Report - .  (2026-09-21)

## Corpus Check
- Corpus is ~25,858 words - fits in a single context window. You may not need a graph.

## Summary
- 453 nodes · 1015 edges · 17 communities (14 shown, 3 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 108 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Community 0
- Community 1
- Community 2
- Community 3
- Community 4
- Community 5
- Community 6
- Community 7
- Community 8
- Community 9
- Community 10
- Community 11
- Community 12
- Community 13
- Community 14
- Community 15
- Community 16

## God Nodes (most connected - your core abstractions)
1. `AccessibilityElement` - 49 edges
2. `AppDelegate` - 32 edges
3. `TaskRunner` - 29 edges
4. `AgentDecision` - 28 edges
5. `ControllerError` - 27 edges
6. `JevClient` - 23 edges
7. `AppTarget` - 21 edges
8. `JevTests` - 19 edges
9. `CDPClient` - 16 edges
10. `RunProgressTests` - 15 edges

## Surprising Connections (you probably didn't know these)
- `rebuild.sh script` --calls--> `build.sh script`  [EXTRACTED]
  rebuild.sh → Scripts/build.sh
- `TaskRunner` --calls--> `RunProgress`  [INFERRED]
  Sources/HolyHand/TaskRunner.swift → Sources/HolyHand/RunProgress.swift
- `Observation` --references--> `AccessibilityElement`  [EXTRACTED]
  Sources/HolyHand/TaskRunner.swift → Sources/HolyHand/AccessibilityElement.swift
- `JevResult` --references--> `AgentDecision`  [EXTRACTED]
  Sources/HolyHand/JevClient.swift → Sources/HolyHand/AgentTypes.swift
- `TaskRunner` --references--> `ActionHistory`  [EXTRACTED]
  Sources/HolyHand/TaskRunner.swift → Sources/HolyHand/AgentTypes.swift

## Import Cycles
- None detected.

## Communities (17 total, 3 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.05
Nodes (44): CFMachPort, CFRunLoopSource, Notification, NSApplication, NSApplicationDelegate, NSHostingView, NSObject, NSPanel (+36 more)

### Community 1 - "Community 1"
Cohesion: 0.08
Nodes (28): Codable, Equatable, AccessibilityElement, .displayLabel, .displayRole, .isOutcomeEvidence, AXUIElement, Bool (+20 more)

### Community 2 - "Community 2"
Cohesion: 0.11
Nodes (23): AnyObject, ControllerError, .errorDescription, TimeInterval, CDPClient, .isConnected, Any, CGRect (+15 more)

### Community 3 - "Community 3"
Cohesion: 0.11
Nodes (17): LocalizedError, ActionHistory, JevClient, JevResult, JevServiceError, .errorDescription, Any, Bool (+9 more)

### Community 4 - "Community 4"
Cohesion: 0.09
Nodes (18): CGFloat, render(), Data, Int, CGImage, CGRect, pid_t, VisionObserver (+10 more)

### Community 5 - "Community 5"
Cohesion: 0.09
Nodes (11): CompletionProtocol, RejectedRequestProtocol, Bool, URLRequest, TextOnlyProtocol, Bool, URLRequest, TextSelectionProtocol (+3 more)

### Community 6 - "Community 6"
Cohesion: 0.12
Nodes (13): Node, Failure, changed, .errorDescription, unavailable, AXUIElement, Bool, Int (+5 more)

### Community 7 - "Community 7"
Cohesion: 0.09
Nodes (16): App, AppKit, ApplicationServices, CGEventTapProxy, CGEventType, CoreGraphics, Scene, ScreenCaptureKit (+8 more)

### Community 8 - "Community 8"
Cohesion: 0.12
Nodes (11): Foundation, Security, ElectronDetector, Bool, Int, pid_t, String, KeychainHelper (+3 more)

### Community 9 - "Community 9"
Cohesion: 0.14
Nodes (14): CheckedContinuation, Error, MainActor, Result, AsyncTimeout, Race, Never, String (+6 more)

### Community 10 - "Community 10"
Cohesion: 0.13
Nodes (12): CGKeyCode, Character, Int32, InputController, AXUIElement, Bool, CGEvent, CGPoint (+4 more)

### Community 11 - "Community 11"
Cohesion: 0.19
Nodes (6): Bool, String, TextEntryPlan, String, TextExtractor, TextEntryPlanTests

### Community 12 - "Community 12"
Cohesion: 0.23
Nodes (8): Date, AXTreeWalker, AnyObject, AXUIElement, Bool, Int, Set, String

### Community 13 - "Community 13"
Cohesion: 0.62
Nodes (7): Decodable, DOMSnapshot, Element, Rect, Screen, Bool, Double

## Knowledge Gaps
- **20 isolated node(s):** `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence`, `.displayRole`, `.displayLabel` (+15 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AccessibilityElement` connect `Community 1` to `Community 2`, `Community 3`, `Community 4`, `Community 7`, `Community 11`, `Community 12`?**
  _High betweenness centrality (0.185) - this node is a cross-community bridge._
- **Why does `TaskRunner` connect `Community 2` to `Community 0`, `Community 1`, `Community 3`?**
  _High betweenness centrality (0.124) - this node is a cross-community bridge._
- **Why does `AppDelegate` connect `Community 0` to `Community 2`, `Community 7`?**
  _High betweenness centrality (0.118) - this node is a cross-community bridge._
- **Are the 14 inferred relationships involving `AgentDecision` (e.g. with `.testOCRLabelsCannotBecomeClickTargetsWithoutVisualGrounding()` and `.testOCRTextUsesExplicitClickTextRatherThanPretendingToBeAButton()`) actually correct?**
  _`AgentDecision` has 14 INFERRED edges - model-reasoned connections that need verification._
- **What connects `PackageDescription`, `notarize.sh script`, `.isOutcomeEvidence` to the rest of the system?**
  _20 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.05365686944634313 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08082706766917293 - nodes in this community are weakly interconnected._