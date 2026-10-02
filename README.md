# HolyHand

> A Windows-native AI desktop assistant featuring a NixOS-inspired Dynamic Island, real-time COM UI automation, offline neural perception, and strict human-in-the-loop safety.

HolyHand connects AI decision intelligence directly to your desktop workflow. Docked at the top of your display as an ambient Dynamic Island, HolyHand stays out of your way until summoned. Click the pill or tap a hotkey to expand the command center, and let HolyHand inspect accessibility trees, evaluate low-risk actions, and execute tasks across your system with verifiable safety guardrails.

---

## Highlights

- **NixOS-Inspired Dynamic Island**:
  - **Idle State**: Collapses into a sleek, top-docked capsule pill (`[ ● HOLYHAND ✦ ]`) with an emerald ready indicator, translucent acrylic glass, and zero intrusive desktop footprint.
  - **Command State**: Expands on click or shortcut into a focused command center with active window targeting, microphone push-to-talk, and instant execution.
  - **Execution State**: Displays live micro-feedback and progress pulses (`⚡ Processing task...`) directly in the island.
  - **Notification Banners**: Transient NixOS-style completion badges (`✓ Launched Spotify`, `✓ Volume set to 50%`) before smoothly settling back to the idle pill.
  - **Persistent & Accessible**: Dismissing (`Esc` or `—`) collapses rather than closes, ensuring HolyHand is never lost.
- **Universal Summoning**:
  - **`Alt` + `Space`**: Standard universal launcher shortcut (Spotlight / Raycast standard).
  - **`Win` + `Shift` + `H`**: Dedicated HolyHand hotkey.
  - **`Ctrl` + `Win`**: Zero-latency low-level modifier chord with Windows Start Menu suppression (`VK 0xE8`).
  - **`Ctrl` + `Shift` + `Space`**: Win32 fallback hotkey.
  - **Tactile Click**: Direct mouse click on the top-center Dynamic Island pill.
- **COM UI Automation**: High-throughput accessibility tree reading via `FlaUI.UIA3` with batched COM `CacheRequest` traversal.
- **Offline Neural Perception**: Local speech recognition via `Whisper.net` and OCR text extraction via `Windows.Media.Ocr`.
- **Calibrated Decision Engine**: Integration with calibrated decision models (`Jev`) via AI Gateway for deterministic, low-risk UI action selection.
- **Guardian Safety Subsystem**: Automatic risk scoring ($0.50$ escalation threshold), password and token redaction, and an unbypassable modal confirmation gate for irreversible operations.

---

## Architecture

```
┌────────────────────────────────────────────────────────┐
│             HolyHand.App (.NET 8 LTS / WPF)            │
│  - Top-Docked Dynamic Island UI (Glassmorphic Capsule) │
│  - Idle Pill [ ● HOLYHAND ✦ ] & Expanded Command Bar   │
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
│  - WindowsAudioService (NAudio WASAPI Endpoint Master) │
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

> **Current Production Runtime**: 100% C# on .NET 8 LTS.  
> **Phase 2 Target**: High-performance native Rust core library.

While HolyHand's production runtime is fully implemented in C# on .NET 8 LTS, a planned **Phase 2 Hybrid Engine** is on the roadmap to deliver deterministic real-time performance:

```
Phase 2 Architecture:
┌─────────────────────────┐          C-ABI          ┌─────────────────────────┐
│     HolyHand.App        │ ◄─────────────────────► │   holyhand_native.dll   │
│   (C# / WPF Frontend)   │    [LibraryImport]      │    (Native Rust cdylib) │
└─────────────────────────┘                         └─────────────────────────┘
  - Dynamic Island UI                                 - Zero-GC kernel keyboard hook
  - UIA3 Accessibility Tree                           - cpal WASAPI low-latency audio
  - Policy & Gateway Client                           - whisper-rs local inference
```

- **Objective**: Move the low-level `WH_KEYBOARD_LL` hook and the raw WASAPI audio streaming loop into an in-process native Rust dynamic library (`holyhand_native.dll`).
- **Motivation**: Guarantees sub-millisecond hook responsiveness immune to GC pause jitter, eliminating Windows OS hook timeout drops (`LowLevelHooksTimeout`).

| Phase | Component | Language / Framework | Status |
|---|---|---|---|
| **Phase 1** | Dynamic Island UI | C# / WPF / XAML | **Production Complete** |
| **Phase 1** | COM UI Automation & Screen Reader | C# / FlaUI.UIA3 | **Production Complete** |
| **Phase 1** | Audio & System Control | C# / NAudio WASAPI | **Production Complete** |
| **Phase 1** | Decision & Guardian Safety Loop | C# / Jev Client / System.Text.Json | **Production Complete** |
| **Phase 2** | Native Low-Level Hook Kernel | Rust (`windows` crate cdylib) | *Future Phase* |
| **Phase 2** | Low-Latency Audio Streaming | Rust (`cpal` WASAPI) | *Future Phase* |
| **Phase 2** | In-Process Neural Whisper Engine | Rust (`whisper-rs`) | *Future Phase* |

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

- **Windows 10** (Build 19041+) or **Windows 11** (x64 / ARM64)
- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (Version 8.0.400+)

### Installation & Build

```powershell
# 1. Clone the repository
git clone https://github.com/harshitthek/HolyHand.git
cd HolyHand

# 2. Build in Release configuration
dotnet build windows/src/HolyHand.App/HolyHand.App.csproj -c Release

# 3. Launch the application
dotnet run --project windows/src/HolyHand.App/HolyHand.App.csproj -c Release
```

### Running Tests

HolyHand includes a comprehensive test suite (364 tests) covering the chord state machine, risk scoring, element ranker, secret sanitization, audio control, and loop guards:

```powershell
dotnet test windows/tests/HolyHand.Tests/HolyHand.Tests.csproj -c Release
```

---

## Usage

1. **Summon HolyHand**:
   - Tap **`Alt` + `Space`** or **`Win` + `Shift` + `H`** from any application.
   - Or click the **[ ● HOLYHAND ✦ ]** pill docked at the top of your display.
2. **Enter Your Task**:
   - Type your task in the Dynamic Island prompt (e.g. `open spotify`, `set volume to 60`, `search for docs on brave`, `check system volume`).
   - Or click **🎤 Mic** for push-to-talk offline transcription.
3. **Execute**:
   - Press **Enter ↵** or click **Run ↵**.
4. **Dismiss or Minimize**:
   - Press **Esc** or click **—** to collapse back to the ambient Dynamic Island pill.
   - Right-click the pill for quick controls (Re-center, Hide, Exit).

---

## Troubleshooting

- **Where is the Dynamic Island?**: Look at the top center of your primary monitor. A small dark pill with an emerald dot `[ ● HOLYHAND ✦ ]` sits at the top edge. Click it or press `Alt + Space` to expand.
- **Hotkeys**: If `Alt + Space` is taken by another app (e.g. PowerToys), use `Win + Shift + H`, `Ctrl + Shift + Space`, or `Ctrl + Win`.
- **Logs**: Rolling diagnostic logs are recorded at `%LocalAppData%\HolyHand\holyhand.log`.
- **Elevated Windows**: Windows UIPI prevents standard applications from automating Administrator windows. To interact with elevated apps, launch HolyHand as Administrator.

---

## License & Attribution

Licensed under the [Apache License, Version 2.0](LICENSE).  
Copyright (c) 2026 harshitthek. All rights reserved.

Pursuant to Section 4(d) of the Apache 2.0 License, author attribution and copyright notices must be retained in all distributions and derivative works. See the [NOTICE](NOTICE) file for formal attribution details.
