# HolyHand — Architecture Decision Records

> Short ADR-style entries for every non-obvious choice made during the port.

---

## ADR-001: .NET 8 LTS over .NET 9

**Date**: 2026-09-21  
**Status**: Accepted  
**Context**: The AGENTS.md spec says "C# on .NET 8 LTS or newer". .NET 8 is LTS (support until Nov 2026). .NET 9 is current but STS (support until May 2026 — already expired or very soon).  
**Decision**: Use .NET 8 LTS (`net8.0-windows10.0.19041.0`) for stability and long-term support.  
**Consequences**: Proven ecosystem, all WPF/WinRT APIs available, no risk of STS expiry.

---

## ADR-002: Vercel AI Gateway vs Direct TypeSafe API

**Date**: 2026-09-21  
**Status**: Accepted  
**Context**: The upstream macOS app uses `https://api.typesafe.ai/v1/systemone` directly with a `noul` question type. The AGENTS.md spec requires using Vercel AI Gateway (`https://ai-gateway.vercel.sh/v1/evaluate`) with `boolean`/`choice`/`score` types.  
**Decision**: Use Vercel AI Gateway as specified. The Gateway adds routing, cost tracking, and `zeroDataRetention` support. The endpoint and question types differ from upstream.  
**Consequences**: Need to verify Gateway response format before M3 completion. Model ID is `typesafe-ai/jev` (configurable). Must confirm that `noul` maps to `boolean` in the Gateway's evaluate endpoint.

---

## ADR-003: Ctrl+Win Chord via Low-Level Hook + State Machine

**Date**: 2026-09-21  
**Status**: Accepted  
**Context**: Upstream uses Ctrl+Space (a regular key + modifier) intercepted via `CGEvent.tapCreate` keyDown event. Windows port needs Ctrl+Win, which is a modifier-only chord. `RegisterHotKey` cannot register modifier-only combinations.  
**Decision**: Use `WH_KEYBOARD_LL` low-level keyboard hook on a dedicated thread with its own message loop. Implement a pure, testable state machine in Core that fires when both Ctrl and Win are held and one is released with no other key pressed in between. Suppress Start menu via VK 0xE8 injection. Provide `RegisterHotKey` fallback (Ctrl+Alt+Space).  
**Consequences**: Hook callback must be minimal (enqueue and return) to avoid Windows removing slow hooks. State machine is fully unit-testable. Need to verify Start menu suppression on Win10 and Win11 empirically.

---

## ADR-004: FlaUI.UIA3 for UI Automation

**Date**: 2026-09-21  
**Status**: Accepted  
**Context**: Need to read app controls on Windows, equivalent to macOS Accessibility API. Options: raw COM-based UIA, `System.Windows.Automation`, FlaUI.  
**Decision**: Use FlaUI.UIA3 with `CacheRequest` to batch property reads. FlaUI provides a clean .NET wrapper over the COM-based UIA3 interface, which is faster and more complete than the managed `System.Windows.Automation` wrapper.  
**Consequences**: `CacheRequest` batching is the main speed lever (single cross-process call vs. one per property). Same caps/priority logic from upstream AXTreeWalker can be applied.

---

## ADR-005: CsWin32 Source Generator for P/Invoke

**Date**: 2026-09-21  
**Status**: Accepted  
**Context**: Need Win32 calls for keyboard hook, SendInput, DPI, window management. Options: hand-written P/Invoke, PInvoke.net signatures, CsWin32 source generator.  
**Decision**: Use `Microsoft.Windows.CsWin32` source generator for type-safe P/Invoke with no hand-written signatures.  
**Consequences**: All Win32 function declarations are generated at build time from `NativeMethods.txt`. Strongly typed, less error-prone.

---

## ADR-006: Serilog with File Sink for Logging

**Date**: 2026-09-21  
**Status**: Accepted  
**Context**: Upstream uses a simple file logger writing to `~/Desktop/holyhand.log`. Windows port needs structured logging with proper redaction.  
**Decision**: Use Serilog with a file sink writing to `%LOCALAPPDATA%\HolyHand\logs`. Use a destructuring policy or enricher to redact API keys from all log output.  
**Consequences**: Standard, testable, supports log rotation and structured output.

---

## ADR-007: Whisper.net for Local Voice Input

**Date**: 2026-09-21  
**Status**: Accepted  
**Context**: Upstream has no voice input. AGENTS.md spec requires local voice transcription.  
**Decision**: NAudio for mic capture + Whisper.net (whisper.cpp bindings) with a small model (tiny/base.en) downloaded on first use. Windows Speech Recognition as fallback.  
**Consequences**: Local, private, no cloud dependency for voice. Model download (~75MB for tiny) on first use adds a one-time setup step.
