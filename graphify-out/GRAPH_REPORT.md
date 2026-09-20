# Graph Report - third-hand  (2026-09-21)

## Corpus Check
- 63 files · ~20,150 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 150 nodes · 171 edges · 34 communities (31 shown, 3 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 1 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- HolyHand.App.csproj
- App
- AccessibilityElement
- CoreInterfaces.cs
- SystemClock
- Program
- HolyHand.Tests.csproj
- ScaffoldTests.cs
- ICredentialStore
- ActionResult

## God Nodes (most connected - your core abstractions)
1. `App` - 12 edges
2. `AccessibilityElement` - 9 edges
3. `Program` - 7 edges
4. `AppTarget` - 7 edges
5. `AgentDecision` - 6 edges
6. `HolyHand.Core.Models` - 5 edges
7. `ActionResult` - 5 edges
8. `SystemClock` - 4 edges
9. `IHotkeyService` - 4 edges
10. `ICredentialStore` - 4 edges

## Surprising Connections (you probably didn't know these)
- `App` --inherits--> `Application`  [EXTRACTED]
  windows/src/HolyHand.App/App.xaml.cs → windows/src/HolyHand.App/App.xaml
- `SystemClock` --implements--> `IClock`  [EXTRACTED]
  windows/src/HolyHand.Core/Common/SystemClock.cs → windows/src/HolyHand.Core/Interfaces/CoreInterfaces.cs

## Import Cycles
- None detected.

## Communities (34 total, 3 thin omitted)

### Community 0 - "HolyHand.App.csproj"
Cohesion: 0.09
Nodes (20): FlaUI.UIA3, H.NotifyIcon.Wpf, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Windows.CsWin32, Serilog, Serilog.Extensions.Logging, Serilog.Sinks.Console, Serilog.Sinks.File (+12 more)

### Community 1 - "App"
Cohesion: 0.12
Nodes (13): bool, HolyHand.App, DebuggerNonUserCodeAttribute, ExitEventArgs, GeneratedCodeAttribute, IServiceCollection, Mutex, ServiceProvider (+5 more)

### Community 2 - "AccessibilityElement"
Cohesion: 0.14
Nodes (12): HolyHand.Core.Models, IntPtr, IReadOnlyList, IDecisionModel, IRiskPolicy, IReadOnlyList, Rectangle, AccessibilityElement (+4 more)

### Community 3 - "CoreInterfaces.cs"
Cohesion: 0.16
Nodes (11): IDisposable, CancellationToken, DateTimeOffset, Task, TimeSpan, IActionExecutor, IAuditLog, IClock (+3 more)

### Community 4 - "SystemClock"
Cohesion: 0.22
Nodes (7): HolyHand.Core.Common, HolyHand.Core.Interfaces, CancellationToken, DateTimeOffset, Task, TimeSpan, SystemClock

### Community 6 - "HolyHand.Tests.csproj"
Cohesion: 0.25
Nodes (7): coverlet.collector, FluentAssertions, Microsoft.NET.Test.Sdk, Moq, xunit, xunit.runner.visualstudio, Microsoft.NET.Sdk

### Community 7 - "ScaffoldTests.cs"
Cohesion: 0.40
Nodes (3): HolyHand.Tests, Fact, ScaffoldTests

## Knowledge Gaps
- **30 isolated node(s):** `H.NotifyIcon.Wpf`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Console`, `Serilog` (+25 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IClock` connect `CoreInterfaces.cs` to `SystemClock`?**
  _High betweenness centrality (0.033) - this node is a cross-community bridge._
- **Why does `SystemClock` connect `SystemClock` to `CoreInterfaces.cs`?**
  _High betweenness centrality (0.024) - this node is a cross-community bridge._
- **Why does `HolyHand.Core.Models` connect `AccessibilityElement` to `CoreInterfaces.cs`?**
  _High betweenness centrality (0.019) - this node is a cross-community bridge._
- **What connects `H.NotifyIcon.Wpf`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` to the rest of the system?**
  _30 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `HolyHand.App.csproj` be split into smaller, more focused modules?**
  _Cohesion score 0.08831908831908832 - nodes in this community are weakly interconnected._
- **Should `App` be split into smaller, more focused modules?**
  _Cohesion score 0.11904761904761904 - nodes in this community are weakly interconnected._
- **Should `AccessibilityElement` be split into smaller, more focused modules?**
  _Cohesion score 0.14210526315789473 - nodes in this community are weakly interconnected._