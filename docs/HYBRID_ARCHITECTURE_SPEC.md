# HolyHand Hybrid Architecture Specification (C# + Rust)

> **Document**: Technical Architecture & Interop Specification  
> **Status**: Approved Blueprint  
> **Components**: `HolyHand.App` (C# .NET 8 LTS) + `holyhand_native` (Rust `cdylib`)

---

## 1. Architectural Overview & Rationale

**HolyHand** adopts a high-performance **hybrid architecture** that marries the strengths of **Rust** (systems-level determinism, zero GC, microsecond OS hooks) with the developer velocity and native Windows integrations of **C# on modern .NET** (first-class COM UI Automation, hardware-accelerated WPF acrylic windowing).

```
┌────────────────────────────────────────────────────────────────────────┐
│                        HolyHand.App (C# / .NET 8 LTS)                  │
│                                                                        │
│   ┌─────────────────────┐   ┌─────────────────┐   ┌────────────────┐   │
│   │  WPF Acrylic Popup  │   │  FlaUI.UIA3     │   │   AgentLoop    │   │
│   │  (Direct3D XAML)    │   │  (COM Reader)   │   │  (Orchestrator)│   │
│   └─────────────────────┘   └─────────────────┘   └────────────────┘   │
└────────────────────────────────────▲───────────────────────────────────┘
                                     │
                        In-Process C-ABI / [LibraryImport]
                     (Zero IPC Overhead, Direct Memory Pointer)
                                     │
┌────────────────────────────────────▼───────────────────────────────────┐
│                     holyhand_native.dll (Rust `cdylib`)                │
│                                                                        │
│   ┌─────────────────────┐   ┌─────────────────┐   ┌────────────────┐   │
│   │   WH_KEYBOARD_LL    │   │ whisper-rs +    │   │  InputInjector │   │
│   │   (0ms GC Hook)     │   │ cpal (WASAPI)   │   │  (SendInput)   │   │
│   └─────────────────────┘   └─────────────────┘   └────────────────┘   │
└────────────────────────────────────────────────────────────────────────┘
```

### Why Hybrid Outperforms Single-Language Implementations:
1. **Zero Hook Timeouts**: Windows removes `WH_KEYBOARD_LL` hooks if callback response exceeds 200–1000ms. In Rust, the hook callback runs on native Win32 threads with zero garbage collection pauses.
2. **No Raw COM Plumbing**: C# utilizes `FlaUI.UIA3` to batch property prefetching via `CacheRequest`, saving thousands of lines of fragile manual COM `IUIAutomation` pointer management.
3. **Hardware-Accelerated Acrylic UI**: WPF XAML renders natively via Direct3D with seamless DWM Desktop Blur (Acrylic/Mica) and native Windows IME support.
4. **Unified In-Process Execution**: No separate daemon processes, no named pipe serialization, and no IPC latency. Rust compiles to a native Windows DLL (`holyhand_native.dll`) loaded directly into the C# process.

---

## 2. Division of Responsibilities

| Subsystem | Owner | Technology | Rationale |
|---|---|---|---|
| **Global Chord Detection** | **Rust** | Win32 `WH_KEYBOARD_LL` + VK `0xE8` suppression | Microsecond execution, 0 GC pauses, deterministic state machine. |
| **Microphone & Speech STT** | **Rust** | `cpal` (WASAPI) + `whisper-rs` (`whisper.cpp`) | Low-latency audio capture, AVX2/CUDA native inference, no NAudio/COM friction. |
| **Hardware Input Injection** | **Rust** | Win32 `SendInput` | Precise timing, hardware mouse/keyboard simulation, direct Win32 structs. |
| **Accessibility Tree Reading** | **C#** | `FlaUI.UIA3` + `CacheRequest` | Mature COM wrapper, batch element fetching in $<50\text{ms}$. |
| **Local OCR Fallback** | **C#** | `Windows.Media.Ocr` (WinRT) | First-class Microsoft SDK projection in `net8.0-windows`. |
| **UI & Windowing** | **C#** | WPF XAML (`AllowsTransparency="True"`) | Sleek dark acrylic popup, tray icon, confirmation modal, DPI awareness. |
| **Agent Orchestration** | **C#** | `AgentLoop` + `HttpClient` | Async/await task pipeline, Vercel AI Gateway HTTP calls, JSON serialization. |
| **Safety Enforcement** | **C#** | `RiskPolicy` (Guardian Mode) | Strict Human-in-the-Loop confirmation gate on sensitive actions. |

---

## 3. FFI & C-ABI Contract

The Rust dynamic library (`holyhand_native.dll`) exports standard `extern "C"` functions consumed by C# via .NET 8 source-generated `[LibraryImport]`.

### 3.1 Hook & Chord Subsystem

#### Rust Interface (`holyhand_native/src/hook.rs`)
```rust
#[repr(C)]
pub struct HookCallbacks {
    pub on_hotkey_triggered: extern "C" fn(),
    pub on_killswitch_triggered: extern "C" fn(),
}

#[no_mangle]
pub unsafe extern "C" fn holyhand_hook_start(callbacks: HookCallbacks) -> bool {
    // Spawns dedicated OS thread with Win32 message pump
    // Installs WH_KEYBOARD_LL hook
    // Handles Ctrl+Win chord & VK 0xE8 Start menu suppression
}

#[no_mangle]
pub unsafe extern "C" fn holyhand_hook_stop() {
    // Posts WM_QUIT to hook thread and unhooks
}

#[no_mangle]
pub unsafe extern "C" fn holyhand_hook_set_run_active(active: bool) {
    // Toggles state machine between Trigger mode and Cancel (Killswitch) mode
}
```

#### C# Interop Declaration (`HolyHand.Platform/Native/NativeHook.cs`)
```csharp
namespace HolyHand.Platform.Native;

public static partial class NativeEngine
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void HotkeyDelegate();

    [StructLayout(LayoutKind.Sequential)]
    public struct HookCallbacks
    {
        public IntPtr OnHotkeyTriggered;
        public IntPtr OnKillswitchTriggered;
    }

    [LibraryImport("holyhand_native.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool holyhand_hook_start(HookCallbacks callbacks);

    [LibraryImport("holyhand_native.dll")]
    public static partial void holyhand_hook_stop();

    [LibraryImport("holyhand_native.dll")]
    public static partial void holyhand_hook_set_run_active([MarshalAs(UnmanagedType.Bool)] bool active);
}
```

---

### 3.2 Hardware Input Simulation Subsystem

#### Rust Interface (`holyhand_native/src/input.rs`)
```rust
#[no_mangle]
pub unsafe extern "C" fn holyhand_input_click(x: i32, y: i32) {
    // Windows SendInput: SetCursorPos + MOUSEEVENTF_LEFTDOWN + MOUSEEVENTF_LEFTUP
}

#[no_mangle]
pub unsafe extern "C" fn holyhand_input_type_utf16(utf16_ptr: *const u16, len: usize) {
    // Windows SendInput: KEYBDINPUT with KEYEVENTF_UNICODE
}

#[no_mangle]
pub unsafe extern "C" fn holyhand_input_send_key(vk: u16) {
    // Virtual key tap
}

#[no_mangle]
pub unsafe extern "C" fn holyhand_input_select_all_and_clear() {
    // Ctrl+A followed by Backspace
}
```

#### C# Interop Declaration (`HolyHand.Platform/Native/NativeInput.cs`)
```csharp
namespace HolyHand.Platform.Native;

public static partial class NativeEngine
{
    [LibraryImport("holyhand_native.dll")]
    public static partial void holyhand_input_click(int x, int y);

    [LibraryImport("holyhand_native.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial void holyhand_input_type_utf16(string text, int len);

    [LibraryImport("holyhand_native.dll")]
    public static partial void holyhand_input_send_key(ushort vk);

    [LibraryImport("holyhand_native.dll")]
    public static partial void holyhand_input_select_all_and_clear();
}
```

---

### 3.3 Audio & Whisper Speech Subsystem

#### Rust Interface (`holyhand_native/src/speech.rs`)
```rust
#[no_mangle]
pub unsafe extern "C" fn holyhand_audio_record_and_transcribe(
    model_path: *const u16,
    out_buffer: *mut u16,
    buffer_capacity: usize,
    out_len: *mut usize
) -> i32 {
    // 1. Records from default WASAPI capture device via cpal
    // 2. Monitors RMS; stops automatically after 1800ms of silence
    // 3. Runs whisper-rs inference using ggml-tiny
    // 4. Writes UTF-16 result into out_buffer
    // Returns 0 on success, negative error code on failure
}
```

---

## 4. Build Pipeline & Packaging

The build pipeline links both tools into a unified distribution artifact:

### Local Development Flow
```powershell
# 1. Build Rust Native Engine
cd rust/holyhand_native
cargo build --release

# 2. Copy DLL to C# build directory
Copy-Item target/release/holyhand_native.dll ../../windows/src/HolyHand.App/ -Force

# 3. Build & Run C# Application
cd ../../windows
dotnet run --project src/HolyHand.App/HolyHand.App.csproj
```

### GitHub Actions CI/CD Pipeline
```yaml
name: Build HolyHand Hybrid Release

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build Rust Engine (cdylib)
        run: cargo build --release --manifest-path rust/holyhand_native/Cargo.toml

      - name: Stage Native DLL
        run: |
          mkdir -p windows/src/HolyHand.App/bin/Release/net8.0-windows10.0.19041.0/
          cp rust/holyhand_native/target/release/holyhand_native.dll windows/src/HolyHand.App/

      - name: Publish Self-Contained App
        run: >
          dotnet publish windows/src/HolyHand.App/HolyHand.App.csproj
          -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
          -o dist/HolyHand-win-x64
```
