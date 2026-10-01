# HolyHand Architecture & Systems Inspection Report

> **Project**: HolyHand (Windows)  
> **Status**: Baseline C# Teardown & Transition to Hybrid Architecture  
> **Target Framework**: .NET 8 LTS (`net8.0-windows10.0.19041.0`)  
> **Primary Platform**: Windows 10 (19041+) & Windows 11 (x64 / ARM64)

---

## 1. Executive Summary & Architectural Evolution

**HolyHand** is a Windows-native desktop AI assistant designed for sub-second, multi-step task execution. Rather than relying on vision models or streaming continuous screenshots to cloud LLMs, HolyHand reads the active application's **native accessibility tree via Windows UI Automation (UIA3)**, constructs a deterministic candidate action space in C#, and uses **TypeSafe Jev** (`typesafe-ai/jev`) through the **Vercel AI Gateway** (`/v1/evaluate`) as a high-precision probabilistic classifier.

### Evolution Path: From Monolithic C# to Hybrid Architecture
1. **Phase 1 (Pure C# Prototype)**: HolyHand was initially engineered entirely in C# on .NET 8 LTS with FlaUI.UIA3, WPF Direct3D windowing, and Whisper.net.
2. **Phase 2 (Architecture Audit & Inspection)**: Runtime benchmarking and stress testing revealed critical areas for improvement: hook timeout sensitivities during GC sweeps, audio buffer copying overhead, and safety policy regressions.
3. **Phase 3 (Hybrid C# + Rust Engine)**: We adopted a hybrid design—delegating low-level system hooks, audio streaming, and hardware input to a zero-GC Rust native core, while keeping UI, agent orchestration, and COM accessibility in C#.

---

## 2. Architecture & Subsystem Teardown

```
┌────────────────────────────────────────────────────────┐
│                   LowLevelKeyboardHook                 │
│         WH_KEYBOARD_LL on dedicated STA thread         │
│          Start-menu suppression via VK 0xE8            │
└───────────────────────────┬────────────────────────────┘
                            │ (Ctrl + Win Trigger)
                            ▼
┌────────────────────────────────────────────────────────┐
│                     PromptPopupWindow                  │
│       WPF Acrylic floating window with drop shadow     │
│       Whisper.net local speech input integration       │
└───────────────────────────┬────────────────────────────┘
                            │ (Task Goal String)
                            ▼
┌────────────────────────────────────────────────────────┐
│                        AgentLoop                       │
│    Orchestrates observation -> decision -> execution   │
└───────┬───────────────────┬───────────────────┬────────┘
        │                   │                   │
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│Screen Reader │    │Decision Model│    │ActionExecutor│
│ FlaUI.UIA3   │    │ Jev Client   │    │ UIA Patterns │
│ CacheRequest │    │ Vercel GW    │    │  SendInput   │
│ Win.Media.Ocr│    │ /v1/evaluate │    │   fallback   │
└──────────────┘    └──────────────┘    └──────────────┘
```

### 2.1 Screen Reading (`HolyHand.Platform/ScreenReading/UiaScreenReader.cs`)
- **Engine**: `FlaUI.UIA3` over Windows COM `IUIAutomation3`.
- **Batch Prefetching (`CacheRequest`)**: Prefetches `Name`, `ControlType`, `IsEnabled`, `BoundingRectangle`, `IsKeyboardFocusable`, `HasKeyboardFocus`, `IsPassword`, and `IsOffscreen` across the subtree in a single cross-process RPC round-trip ($<50\text{ms}$).
- **Secret Redaction (`SecretSanitizer.cs`)**:
  - `IsPassword=true` controls are unconditionally replaced with `[PASSWORD]`.
  - Regex masks credit card patterns (`\b\d{4}[ -]?\d{4}...\b`), API keys (`vck_*`, `sk-*`, `ghp_*`), and `Bearer` tokens.
- **OCR Fallback (`WindowsOcrService.cs`)**:
  - If interactive element count is below threshold (e.g. custom canvases, games, unlabelled Electron surfaces), captures the window bounds via GDI `CopyFromScreen` and executes **`Windows.Media.Ocr`** locally on-device. Zero cloud vision calls.

### 2.2 Global Hotkey Hook (`HolyHand.Platform/Hotkey/LowLevelKeyboardHook.cs`)
- **Mechanism**: Win32 `WH_KEYBOARD_LL` installed via `PInvoke.SetWindowsHookEx`.
- **Dedicated Thread**: Runs on an isolated STA thread with its own native Win32 message pump (`GetMessage`/`DispatchMessage`).
- **Start Menu Suppression**: Detects Windows key release while chord is armed and injects dummy unassigned virtual key tap `0xE8` to suppress the Windows Start Menu.
- **Channel Decoupling**: Hook callback writes to an unbounded `Channel<RawKeyEvent>` and returns immediately ($<0.1\text{ms}$), avoiding OS hook timeouts.

### 2.3 Jev Decision Model (`HolyHand.Core/Jev/JevDecisionModel.cs`)
- **Dynamic Candidate Generation**:
  - Reads screen elements, filters for clickable roles (`Button`, `MenuItem`, `Hyperlink`, `TabItem`, `CheckBox`, etc.).
  - Extracts text arguments from the user goal (quoted strings, terms following verbs like `search for`, `type`, `play`, `calculate`).
  - Generates discrete candidate actions (`click:e1`, `type_and_enter:e2:Adele`, `open_app:spotify`, `scroll:down`, `done`, `ask_user`).
- **Structured Gateway Request**:
  - Sends state (goal, app, window, recent actions, element list) to Vercel AI Gateway (`/v1/evaluate`).
  - Questions: `nextAction` (`choice` over generated list) and `goalAchieved` (`boolean`).

### 2.4 Action Execution (`HolyHand.Platform/Execution/ActionExecutor.cs`)
- **Foreground Verification**: Before physical execution, verifies that foreground PID matches expected target, aborting if the user switched windows mid-run.
- **Pattern Execution**:
  1. Tries COM UIA patterns: `InvokePattern.Invoke()`, `TogglePattern.Toggle()`, `SelectionItemPattern.Select()`, or `ValuePattern.SetValue()`.
  2. If COM patterns fail or are unsupported, falls back to hardware-level `PInvoke.SendInput` clicks and `KEYEVENTF_UNICODE` typing.
- **Auto-Submission**: Automatically injects `Enter` after typing into controls identified as address bars or search boxes.
- **Clear Before Type**: Injects `Ctrl+A` then `Backspace` before typing to prevent infinite character append loops.

### 2.5 Speech Recognition (`HolyHand.Platform/Speech/WhisperSpeechService.cs`)
- **Audio Capture**: `NAudio` with WASAPI / WaveIn event streaming.
- **Engine**: Local `Whisper.net` bindings with `ggml-tiny.bin` model (~75MB) downloaded on first start.
- **Silence Detection**: Automatically stops recording after $1800\text{ms}$ of audio below $0.01$ RMS. Zero cloud dependency.

### 2.6 Credential Security (`HolyHand.Platform/Safety/CredentialStore.cs`)
- Stores Vercel AI Gateway keys inside **Windows Credential Manager** via `advapi32.dll` (`CredReadW`/`CredWriteW`) under `HolyHand/AI_GATEWAY_API_KEY`.

---

## 3. Critical Findings & Security Vulnerabilities

### Finding 1: Leaked Production API Key in Git Commit Tree
* **Location**: Commit `20e2d4e` (`feat(m4): screen reading with UIA3...`) in file `windows/tests/HolyHand.Tests/ScreenReading/ScreenReaderTests.cs`.
* **Leaked Token**: `vck_2eYKfzon...[REDACTED_HISTORICAL_TOKEN]`.
* **Status**: While commit `9524bdf` replaced this token in the working tree with a dummy string, **the real production key remained embedded in the original Git history**. Anyone cloning the upstream repository could extract the key via `git log -S "vck_"`.
* **Remediation**: In HolyHand, this token has been completely purged and replaced across all historical Git commits.

---

### Finding 2: Severe Safety Invariant Regression ("Jarvis Mode" vs README)
* **README Promise**:
  > *"Before any critical or irreversible step (Submit, Apply, Send, Pay, Delete, Post, Install, Confirm), HolyHand pauses and asks you to approve. Routine steps execute automatically."*
* **Actual Implementation in Code (`RiskPolicy.cs`)**:
  In commit `a52d03a` (*"feat(safety): Jarvis mode — zero confirmation dialogs, only deletions blocked"*), the author deliberately disabled confirmation prompts:
  ```csharp
  // HolyHand.Core/Safety/RiskPolicy.cs
  public bool RequiresConfirmation(AgentDecision decision, AccessibilityElement? target, AppTarget appTarget, out string reason)
  {
      // Jarvis mode: zero confirmation dialogs — always auto-execute
      reason = string.Empty;
      return false;
  }
  ```
  - `RiskPolicyOptions.SensitiveVerbs` was completely emptied.
  - The confirmation dialog is rendered completely dead code.
  - Tests in `RiskPolicyTests.cs` and `MockJobPageTests.cs` were altered to assert that **"Submit Application"**, **"Pay $50"**, **"Buy License"**, and **"Transfer Funds"** execute **automatically with zero human intervention**.
  - Only string regex matches on `delete`, `deletion`, `erase`, `wipe`, `destroy`, `truncate`, `format`, `del` are blocked.

---

### Finding 3: Removal of Execution Bounds & Confidence Gating
* **Unbounded Steps**: In commit `ea4b1a3`, `MaxSteps` was changed to `0` (unlimited), allowing the agent loop to spin indefinitely if the UI state oscillates.
* **Confidence Floor Bypassed**: In commit `bcd70d4`, probability threshold checking was removed from `JevDecisionModel.cs`. Low-confidence decisions execute immediately instead of triggering `ask_user`.

---

### Finding 4: UIPI Privilege Boundary
* If the target application is running as Administrator (elevated), standard-user processes cannot send input or read UIA trees due to Windows User Interface Privilege Isolation (UIPI).
* `UiaScreenReader` detects elevation and throws `InvalidOperationException`, but the application does not prompt for self-elevation or inform the user how to resolve the permission disparity.

---

## 4. Architectural Scorecard

| Criterion | Score | Assessment |
|---|:---:|---|
| **Windows API Mastery** | **9.5 / 10** | Exceptional use of `CsWin32`, `WH_KEYBOARD_LL`, VK `0xE8` Start suppression, and UIA `CacheRequest`. |
| **Execution Speed** | **9.0 / 10** | Sub-50ms screen reads, ~200ms Jev decisions, ReadyToRun AOT startup. |
| **Code Structure & Tests** | **8.5 / 10** | Clean Architecture, dependency injection, 71 thorough unit/integration tests with Moq and FluentAssertions. |
| **Safety Invariants** | **3.0 / 10** | Critical regression: "Jarvis Mode" gutted confirmation gates; README contradicts code. |
| **Secret Management** | **4.0 / 10** | Native Credential Manager is good, but leaked key in Git history is a high-severity issue. |
