# HolyHand

> A Windows-native AI desktop assistant with real-time UI automation, offline perception, and strict human-in-the-loop safety.

HolyHand connects AI decision intelligence directly to your desktop. Focus any application, summon HolyHand via the top-docked Dynamic Island, and let it inspect controls, calculate low-risk actions, and execute tasks across your system with verifiable safety guardrails.

---

## Overview

HolyHand is built natively for Windows 10 and 11 on **.NET 8 LTS**, leveraging deep OS accessibility APIs and local neural perceptual models:

- **Dynamic Island Overlay**: A sleek, top-docked capsule interface inspired by modern Wayland and dynamic notification surfaces.
- **Dual Hotkey Activation**:
  - **`Ctrl` + `Win`**: Zero-latency low-level modifier chord with Windows Start Menu suppression (`VK 0xE8`).
  - **`Ctrl` + `Shift` + `Space`**: Native Win32 `RegisterHotKey` fallback.
- **COM UI Automation**: High-throughput accessibility tree reading via `FlaUI.UIA3` with batched COM `CacheRequest` traversal.
- **Offline Perception**: Local speech recognition via `Whisper.net` and OCR text detection via `Windows.Media.Ocr`.
- **TypeSafe Decision Engine**: Integration with calibrated decision models (`Jev`) via AI Gateway for deterministic, low-risk action selection.
- **Guardian Safety Subsystem**: Automatic risk scoring ($0.50$ escalation threshold), password and token redaction, and an unbypassable modal confirmation gate for irreversible operations.

---

## Architectural Architecture

```
┌────────────────────────────────────────────────────────┐
│             HolyHand.App (.NET 8 LTS / WPF)            │
│  - Top-Docked Dynamic Island UI (Glassmorphic Capsule) │
│  - Single-Instance Activation & Event Signaling        │
│  - Native Win32 RegisterHotKey & IPC Event Dispatcher  │
│  - Guardian Human-in-the-Loop Modal Approval Gate      │
└───────────────────────────▲────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│         HolyHand.Platform (Win32 & OS Integration)     │
│  - Low-Level Keyboard Hook (WH_KEYBOARD_LL)            │
│  - Start Menu Dummy Tap Suppression (VK 0xE8)          │
│  - FlaUI.UIA3 Accessibility Tree Inspector             │
│  - Windows.Media.Ocr Text Perception Service           │
│  - Whisper.net Local Offline Speech-to-Text            │
│  - Input Simulation & Window Capture Pipeline          │
└───────────────────────────▲────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│           HolyHand.Core (Domain & Safety Policy)       │
│  - ChordStateMachine Pure State Transition Model       │
│  - Risk Policy & Irreversible Verb Classifier          │
│  - Secret Sanitizer (Tokens, Keys, Credentials)        │
│  - Agent Loop, Stalling Detector, Audit Logger         │
└────────────────────────────────────────────────────────┘
```

---

## Roadmap: Future Phase Hybrid Rust Engine

While HolyHand's current production runtime is 100% C# on .NET 8 LTS, a planned **Phase 2 Hybrid Engine** is on the architectural roadmap:

```
Future Phase:
┌─────────────────────────┐          C-ABI          ┌─────────────────────────┐
│     HolyHand.App        │ ◄─────────────────────► │   holyhand_native.dll   │
│   (C# / WPF Frontend)   │    [LibraryImport]      │    (Native Rust cdylib) │
└─────────────────────────┘                         └─────────────────────────┘
  - Dynamic Island UI                                 - Zero-pause kernel hook
  - UIA3 Accessibility Tree                           - cpal WASAPI low-latency
  - Policy & Gateway Client                           - whisper-rs inference
```

- **Objective**: Move the low-level `WH_KEYBOARD_LL` hook and the raw WASAPI audio streaming loop into an in-process native Rust dynamic library (`holyhand_native.dll`).
- **Motivation**: Guarantees sub-millisecond hook responsiveness immune to GC pause jitter, eliminating Windows OS hook timeout drops (`LowLevelHooksTimeout`).

---

## Safety Invariants

HolyHand enforces non-negotiable safety constraints on all automated interactions:

1. **Human Confirmation on Irreversible Verbs**: Any action attempting sensitive operations (*Submit, Apply, Send, Pay, Buy, Transfer, Post, Install, Run, Confirm*) halts the execution loop and displays an explicit modal approval dialog.
2. **Hard Deletion Prohibition**: Tasks targeting destructive actions (*Delete, Erase, Wipe, Destroy, Truncate, Format*) are blocked unconditionally at the policy layer.
3. **Secret Redaction**: Password fields (`IsPassword=true`), API tokens (`sk-*`, `ghp_*`, `vck_*`), and authentication cookies are redacted prior to decision model evaluation and audit logging.
4. **Target Deny-List**: Security-sensitive software (e.g. 1Password, Bitwarden, KeePass, Windows Security) is completely excluded from automation.
5. **Instant Kill Switch**: Pressing **Esc** or repeating the activation chord aborts execution immediately within $<1\text{ms}$.

---

## Getting Started

### Prerequisites

- Windows 10 (Build 19041+) or Windows 11 (x64 / ARM64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or VS Code with C# Dev Kit (optional)

### Build & Run

```powershell
# 1. Clone the repository
git clone https://github.com/harshitthek/HolyHand.git
cd HolyHand

# 2. Build the solution in Release configuration
dotnet build windows/src/HolyHand.App/HolyHand.App.csproj -c Release

# 3. Launch the application
dotnet run --project windows/src/HolyHand.App/HolyHand.App.csproj -c Release
```

### Running Tests

HolyHand includes comprehensive unit test suites covering the chord state machine, risk scoring, element ranker, secret sanitization, and loop guards:

```powershell
dotnet test windows/tests/HolyHand.Tests/HolyHand.Tests.csproj -c Release
```

---

## Usage

1. **Summon HolyHand**:
   - Press **`Ctrl` + `Win`** (press both and release), OR
   - Press **`Ctrl` + `Shift` + `Space`**.
2. **Enter Your Task**:
   - Type your task in the Dynamic Island prompt (e.g. `search for Adele on youtube`, `open notepad and write hello world`, `check system volume`).
   - Or click **🎤 Mic** for push-to-talk offline transcription.
3. **Execute**:
   - Press **Enter** or click **Run ↵**.
4. **Dismiss or Cancel**:
   - Press **Esc** to dismiss the island or cancel an active task.

---

## License & Attribution

Licensed under the [Apache License, Version 2.0](LICENSE).
Copyright (c) 2026 harshitthek. All rights reserved.

Pursuant to Section 4(d) of the Apache 2.0 License, author attribution and copyright notices must be retained in all distributions and derivative works. See the [NOTICE](NOTICE) file for formal attribution details.
