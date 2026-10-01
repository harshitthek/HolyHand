# HolyHand (Windows)

> A blazing-fast, safe, Windows-native AI desktop assistant. Focus an app, press **Ctrl + Win**, and let it take the wheel with strict human oversight.

HolyHand reads accessible UI controls via Windows UI Automation, selects the optimal actions using the **TypeSafe Jev** decision engine via **Vercel AI Gateway**, types, clicks, and verifies the outcome in real time.

---

## Architecture: Hybrid C# + Rust

HolyHand uses a high-performance **hybrid architecture** that eliminates runtime compromises:

```
┌────────────────────────────────────────────────────────┐
│             HolyHand.App (C# on .NET 8 LTS)            │
│  - Modern WPF Direct3D Acrylic Floating Popup          │
│  - System Tray Integration & Notification Hooks        │
│  - FlaUI.UIA3 COM UI Automation with CacheRequest      │
│  - Windows.Media.Ocr Local WinRT Fallback              │
│  - Guardian Human-in-the-Loop Safety Gates             │
└───────────────────────────▲────────────────────────────┘
                            │
              In-Process C-ABI / [LibraryImport]
              (Zero IPC Overhead, Zero Latency)
                            │
┌───────────────────────────▼────────────────────────────┐
│          holyhand_native.dll (Native Rust `cdylib`)    │
│  - Low-Level Keyboard Hook (WH_KEYBOARD_LL)            │
│  - Zero GC Pauses, Zero OS Hook Timeouts               │
│  - Windows Start-Menu Suppression (VK 0xE8)            │
│  - Offline Whisper.cpp Speech Recognition (cpal/WASAPI)│
│  - Hardware Input Simulation (SendInput)               │
└────────────────────────────────────────────────────────┘
```

---

## Key Differentiators & Improvements over HolyHand

| Feature | HolyHand (Baseline) | HolyHand (Our Engine) |
|---|---|---|
| **Keyboard Hook Engine** | C# Thread (prone to GC pause timeouts) | **Pure Rust Native Engine** (Zero GC, sub-microsecond latency) |
| **Speech Recognition** | NAudio + Whisper.net (wrapper overhead) | **Native `cpal` (WASAPI) + `whisper-rs`** statically linked in Rust |
| **Safety Invariants** | Regressed: "Jarvis Mode" bypassed all confirmation dialogs | **Strict Guardian Mode**: Hardcoded human approval on sensitive verbs |
| **Step Bounding** | Unbounded (`MaxSteps = 0`) | **Bounded (`MaxSteps = 25`)** + SHA-256 state-hashing loop guard |
| **Confidence Floor** | Bypassed (executed low-confidence guesses) | **Calibrated Gating**: Automatically falls back to user on ambiguity |
| **Secret Hygiene** | Production API key leaked in git commit tree | **Clean Git Tree** + Windows Credential Manager DPAPI isolation |

---

## Safety Invariants (Non-Negotiable)

1. **Human Confirmation for Irreversible Steps**: Any action involving sensitive verbs (*Submit, Apply, Send, Pay, Buy, Transfer, Post, Install, Run, Confirm*) unconditionally halts execution and displays the modal approval dialog.
2. **Hard Deletion Prohibition**: Tasks or actions attempting data destruction (*Delete, Erase, Wipe, Destroy, Truncate, Format*) are hard-blocked at the policy layer.
3. **On-Screen Secret Redaction**: Passwords (`IsPassword=true`), credit card numbers, API keys (`vck_*`, `sk-*`, `ghp_*`), and authorization tokens are scrubbed before model submission or audit logging.
4. **App Deny-List**: Password managers (1Password, Bitwarden, KeePass, etc.) and admin security utilities are strictly blocked from automation.
5. **Instant Kill Switch**: Pressing **Ctrl + Win** again or pressing **Esc** aborts active runs immediately within $<1\text{ms}$.

---

## Documentation Index

- [HolyHand Deep Inspection Report](docs/HOLYHAND_INSPECTION.md): Complete architectural breakdown, code analysis, and security vulnerability findings of the original codebase.
- [Hybrid Architecture Specification](docs/HYBRID_ARCHITECTURE_SPEC.md): Technical interop contract, C-ABI signatures, Rust `cdylib` layout, and .NET 8 `[LibraryImport]` bindings.
- [Safety & Security Specification](docs/SECURITY_AND_SAFETY_SPEC.md): Detailed risk taxonomy, Guardian confirmation modal, step limits, and privacy filters.

---

## Building from Source

### Prerequisites
- Windows 10 (build 19041+) or Windows 11 (x64 or ARM64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Rust Toolchain (MSVC target)](https://rustup.rs/)

### Build & Run
```powershell
# 1. Build the Rust Native Engine
cd rust/holyhand_native
cargo build --release

# 2. Stage the native DLL
Copy-Item target/release/holyhand_native.dll ../../windows/src/HolyHand.App/ -Force

# 3. Build & Run the C# Application
cd ../../windows
dotnet run --project src/HolyHand.App/HolyHand.App.csproj
```

---

## License

Licensed under the [MIT License](LICENSE).
