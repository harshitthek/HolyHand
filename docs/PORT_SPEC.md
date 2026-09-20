# HolyHand → HolyHand: Port Specification

> Generated during M0 recon. Every upstream Swift module/file is mapped to its
> responsibility, the Windows equivalent, and whether the code is portable,
> needs a full rewrite, or can be dropped.

## 1. Upstream Source Files

### 1.1 App Shell & Lifecycle

| File | Responsibility | Windows Equivalent | Disposition |
|---|---|---|---|
| `HolyHandApp.swift` (23 lines) | SwiftUI `@main` entry point; `MenuBarExtra` with menu items (Run, Status, Set API Key, Quit) | WPF `App.xaml.cs` + `H.NotifyIcon.Wpf` tray icon with context menu | **Rewrite** |
| `AppDelegate.swift` (267 lines) | App lifecycle coordinator: hotkey handling, overlay show/hide, task start/cancel/delegate, setup UI (SwiftUI `SetupView`), API key prompt (NSAlert + NSSecureTextField), permission polling | `App.xaml.cs` composition root with DI container, `IHotkeyService` events → coordinator, WPF popup/confirmation, first-run key setup dialog | **Rewrite** |
| `Logger.swift` (22 lines) | Simple file + stdout logger writing to `~/Desktop/holyhand.log` | **Serilog** file sink to `%LOCALAPPDATA%\HolyHand\logs`, structured logging | **Rewrite** |

### 1.2 Hotkey & Input

| File | Responsibility | Windows Equivalent | Disposition |
|---|---|---|---|
| `HotkeyManager.swift` (89 lines) | `CGEvent.tapCreate` for keyDown events; intercepts Ctrl+Space (keyCode 49 with maskControl, no other modifiers); fires on main queue; auto-re-enables on timeout | `WH_KEYBOARD_LL` hook on dedicated thread with message pump; **pure state machine** for Ctrl+Win chord (both held, one released, no other key in between); Start menu suppression via VK 0xE8 injection; fallback `RegisterHotKey` (Ctrl+Alt+Space) | **Rewrite** |
| `InputController.swift` (87 lines) | `CGEvent`-based click (single/double/right), keypress with modifiers, Unicode typing (per-character with cancellation checks), scroll; key code map; `frame()` helper for AX element bounds | `SendInput` via CsWin32 (MOUSEINPUT/KEYBDINPUT); UIA patterns preferred (Invoke, Value, Toggle, ExpandCollapse, Scroll); Unicode typing via `SendInput` with `KEYEVENTF_UNICODE`; key code map for Windows VKs | **Rewrite** |

### 1.3 UI

| File | Responsibility | Windows Equivalent | Disposition |
|---|---|---|---|
| `OverlayPanel.swift` (160 lines) | Borderless `NSPanel` overlay covering the target window; blur+dark background; centered SwiftUI input (app icon, name, prompt, text field, Enter to submit, Esc to cancel); auto-focuses text field | WPF borderless topmost `Window` (pre-created, hidden at startup); acrylic/blur effect or semi-transparent dark overlay; text box + mic button + Send + Stop + status line; follows system light/dark theme; per-monitor DPI v2 | **Rewrite** |
| `StatusIndicatorWindow.swift` (131 lines) | Floating non-activating `NSPanel` near target window's top-right; shows spinner + status text + × button; `showDone()` auto-dismisses after 1.5s; `showError()` resizes to fit error text, stays until dismissed | WPF floating non-activating window; same positioning logic; spinner/progress, status line, × button; done/error states | **Rewrite** |

### 1.4 Screen Reading

| File | Responsibility | Windows Equivalent | Disposition |
|---|---|---|---|
| `AccessibilityElement.swift` (57 lines) | Model struct: `id`, `role`, `label`, `value`, `enabled`, `actions`, `axElement`, `frame`, `focused`, `source`; computed `displayRole` (strips "AX" prefix), `displayLabel`, `isOutcomeEvidence`; `screenFrame()` from AX attributes; `compactDescription()` | Same model in C# (record or class). Roles change from `AXButton`→`Button`, etc. (UIA `ControlType`). `isOutcomeEvidence` logic is portable. `screenFrame()` from UIA `BoundingRectangle` property. | **Port** (adapt role names) |
| `AXTreeWalker.swift` (140 lines) | Walks AX element tree with `AXUIElementCopyMultipleAttributeValues` batch reads; 0.8s time budget, 30 depth cap, 1200 visit limit, 500 element cap; skips container roles; prioritizes focused/interactive over static text; filters off-screen elements | FlaUI.UIA3 `CacheRequest` to batch property reads (`Name`, `ControlType`, `IsEnabled`, `BoundingRectangle`, `IsKeyboardFocusable`, etc.); same caps and priority logic; `TreeWalker` with `Condition` filters | **Rewrite** |
| `VisionObserver.swift` (78 lines) | Apple Vision `VNRecognizeTextRequest` on a screenshot from `WindowSnapshot.capture()`; maps bounding boxes to screen coordinates; merges OCR text with AX controls (deduplicates overlapping regions with same label) | `Windows.Media.Ocr.OcrEngine` (WinRT, built-in); screenshot via `PrintWindow`/`BitBlt`; same merge logic | **Rewrite** |
| `WindowSnapshot.swift` (67 lines) | `CGWindowListCopyWindowInfo` to find front window by PID; `selectWindow()` matches AX focused window frame; `SCScreenshotManager.captureImage()` for OCR screenshots | Win32 `EnumWindows` + `GetWindowThreadProcessId`; `DwmGetWindowAttribute` for frame; `PrintWindow` or `BitBlt` for screenshots | **Rewrite** |
| `AppTarget.swift` (75 lines) | Captures frontmost app: PID, name, bundle ID, `NSRunningApplication`, `AXUIElement` for app and focused window, window frame, icon; filters self and system agents | Win32 `GetForegroundWindow`, `GetWindowThreadProcessId`, process name/path via `QueryFullProcessImageName`; UIA `AutomationElement` from HWND; window rect via `GetWindowRect`/`DwmGetWindowAttribute`; process icon via `ExtractIcon` | **Rewrite** |

### 1.5 Agent Loop & Decision

| File | Responsibility | Windows Equivalent | Disposition |
|---|---|---|---|
| `TaskRunner.swift` (384 lines) | Main agent loop: observe → decide → validate → execute → verify → repeat. 30-step max, 40-iteration observation budget, 180s global timeout. Focus checking, stale-decision detection, OCR recovery, text entry planning, field focus confirmation, settle (2.5s polling), verification. Delegates to `JevClient` for decisions and `InputController` for execution. | Same loop structure in `HolyHand.Core` with `IScreenReader`, `IDecisionModel`, `IActionExecutor`, `IConfirmationPrompt`. All platform I/O through interfaces. Add `IRiskPolicy` deterministic check before Jev risk call. **Dry-run by default until M6.** | **Port** (core logic portable, platform calls via interfaces) |
| `JevClient.swift` (390 lines) | Three API calls: (1) `decide()` — builds request with element list, target ranking, operation choices, sends to `/v1/systemone`; (2) `selectText()` — two-phase text selection (intent then content); (3) `confirmCompletion()` — done-only verification call. Request prep with size budget (24KB), element compaction, label truncation. Decode with target validation. Error detail extraction with key redaction. | Jev `/v1/evaluate` client via Vercel AI Gateway. Map `noul` → `boolean`, keep `choice`. Same request structure adapted to Gateway format. `IHttpClientFactory` + Polly for resilience. Same request budget logic. | **Rewrite** (different API endpoint + format) |
| `AgentTypes.swift` (69 lines) | `AgentDecision` (operation, targetIndex, textValue, x, y, key, modifiers, reason); `validate()` method checking operation validity, target existence/enablement, role compatibility, coordinate bounds; `ControllerError.invalid`; `ActionHistory` | C# records. Validation logic is portable. Operations may change names (AX roles → UIA roles). | **Port** |
| `RunProgress.swift` (99 lines) | `ObservationState.signature()` — sorted string of role/label/value/enabled/focused/position for repeat detection. `matching()` — find same element across snapshots. `verify()` — check if action had effect. `RunProgress` — loop guard tracking pairs, actions, failed targets, failure count, recovery state. | **Directly portable** — pure logic, no platform dependencies. | **Port directly** |
| `TextEntryPlan.swift` (49 lines) | `candidates()` — extract literal/search phrases from goal (regex for quoted text, word n-grams, capped at 100). `build()` — validate kind (search/literal) and content (no newlines, no NUL). `isTerminal()` — detect terminal bundles. | **Directly portable**. `isTerminal()` needs Windows process name equivalents (cmd.exe, powershell.exe, Windows Terminal, etc.). | **Port** |
| `TextExtractor.swift` (20 lines) | Regex-based extraction: `type "..."`, `search for "..."`, `search for ...`. Rejects compound requests with "and/then/into". | **Directly portable** — pure regex. | **Port directly** |
| `TextFieldFocus.swift` (86 lines) | `contains()` — walk parent chain to confirm focus is within target. `confirmed()` — check AX focused element. `prepare()` — request AX focus → wait → click fallback → wait. `wait()` — poll with cancellation/check hooks. | Same pattern with UIA `FocusedElement` + parent walk. Click fallback via `SendInput`. | **Port** (adapt API) |
| `AsyncTimeout.swift` (47 lines) | Race pattern: work task vs timer task, first to finish wins. Supports cancellation. | `CancellationTokenSource.CreateLinkedTokenSource` + `Task.WhenAny`. | **Rewrite** |

### 1.6 Browser Support (M9)

| File | Responsibility | Windows Equivalent | Disposition |
|---|---|---|---|
| `CDPClient.swift` (267 lines) | Chrome DevTools Protocol WebSocket client; discovers CDP target via HTTP; injects DOM extraction JS; maps DOM elements to `AccessibilityElement` model with screen coordinates | Same concept — optional M9. Consider Playwright/CDP. | **Port later (M9)** |
| `ElectronDetector.swift` (70 lines) | Detect Electron apps by checking for `Electron Framework.framework`; find `--remote-debugging-port=` in process args via `/bin/ps`; verify port ownership via `/usr/sbin/lsof`; probe port | Windows: check for `electron.exe` in process path; parse command line via WMI/`NtQueryInformationProcess`; verify port via `netstat` equivalent | **Port later (M9)** |

### 1.7 Build Scripts (Drop)

| File | Responsibility | Disposition |
|---|---|---|
| `Scripts/build.sh` | macOS code-signing build | **Drop** (replaced by `dotnet publish`) |
| `Scripts/notarize.sh` | Apple notarization | **Drop** |
| `Scripts/generate-icon.swift` | macOS icon generation | **Drop** (use standard Windows .ico) |
| `Resources/Info.plist` | macOS app metadata | **Drop** (replaced by `.csproj` properties) |
| `Resources/AppIcon.*` | macOS app icons | **Drop** (need new Windows .ico) |
| `rebuild.sh` | Build shortcut | **Drop** |

---

## 2. Prompt / Candidate / Verification Logic to Preserve

### 2.1 Request Structure (from `JevClient.requestBody()`)

**State object:**
```json
{
  "task": "<user goal>",
  "app": "<app name, max 100 chars>",
  "step": <1-based step number>,
  "action_attempts": ["<action>: <result>", ...],  // last 8, or ["nothing yet"]
  "observationMayBeTruncated": <bool>,
  "targetChoicesShortlisted": <bool>,
  "observedElementCount": <total count>,
  "elements": [
    {"id": "<stable id>", "label": "<display label, max 160>", "role": "<display role>",
     "enabled": <bool>, "focused": <bool>, "source": "accessibility|ocr",
     "value": "<if different from label, max 160>"}
  ]
}
```

**Questions:**
- `done` (`noul`/boolean): "Has this task been completed: \"{goal}\"? Judge only by what is visible on screen and actions already taken."
- `absent` (`noul`/boolean): "Is the control needed for the next step of \"{goal}\" missing from the elements on screen?"
- `operation` (choice): Operations = {SCROLL_UP, SCROLL_DOWN, PRESS_RETURN, PRESS_TAB, PRESS_ESCAPE, WAIT, DONE, BLOCKED} + {CLICK, TYPE_TEXT, CLICK_TEXT if targets exist}
- `{op}_target` (choice): Per-operation target selection with `__none__` option. Criteria = `"{label} = {value} [{role}]"` per candidate.

### 2.2 Target Building (from `JevClient.targets()` + `offeredTargets()`)

**Click targets:** AXButton, AXMenuItem, AXMenuBarItem, AXLink, AXTab, AXCheckBox, AXRadioButton, AXPopUpButton, AXRow, AXCell, AXDisclosureTriangle, AXSwitch — OR has actions (AXPress, AXOpen, AXConfirm, AXPick)

**Type targets:** AXTextField, AXTextArea, AXComboBox (also clickable)

**OCR targets:** Source == "ocr" with frame → CLICK_TEXT (separate from CLICK)

**Ranking:** Score = (goal keyword overlap × 10) + (focused × 5). Top 254 per operation + `__none__`.

### 2.3 Request Size Budget

- Max request: 24,000 bytes
- Max goal: 4,000 UTF-8 bytes
- Element labels/values truncated to 160 chars
- History entries truncated to 256 chars each, last 4 kept
- If over budget, halve element list and retry

### 2.4 Text Selection (from `JevClient.selectText()`)

Two-phase Jev call:
1. **Intent call**: choice over `{literal, search, unsupported}`. Instructions distinguish search (shortest entity name/keywords) from literal (exact user-supplied wording).
2. **Content call**: choice over `TextEntryPlan.candidates()` + `none`. State includes `selectedIntent`.

### 2.5 Completion Verification (from `JevClient.confirmCompletion()`)

Separate call with only `done` question. Stricter instructions: "Are ALL requirements of the task already satisfied by the current screen and recorded actions?" Threshold: `done >= 0.70`.

### 2.6 Loop Guard (from `RunProgress`)

- Signature = sorted `source|role|displayLabel|value|enabled|focused|position(8px grid)` per element
- Pair = action description + screen signature
- Exact pair repeat → blocked
- Failed target state tracking across OCR recovery
- 2 consecutive unverified actions → recovery
- Same non-scroll action 3× → blocked
- Single OCR recovery attempt allowed

### 2.7 Verification (from `ObservationState.verify()`)

- TYPE_TEXT: exact value match in the correct field
- CLICK: target gained focus = verified
- General: signature changed = verified, unchanged = not verified
- Settle: poll up to 2.5s, require 1s minimum + 400ms quiet window

---

## 3. Windows-Specific Additions (not in upstream)

| Capability | Details |
|---|---|
| Ctrl+Win chord state machine | Pure, testable; no equivalent in upstream (upstream uses simple Ctrl+Space keyDown) |
| Start menu suppression | VK 0xE8 injection before Win key release passes through |
| Kill switch (second press = cancel) | Upstream has this but simpler; Windows needs hook-level cancel detection |
| `IRiskPolicy` deterministic check | Upstream has no explicit risk policy — all execution is automatic. Windows port adds safety layer. |
| Confirmation dialog | New WPF dialog showing action/target/window with Approve/Reject |
| Audit log (JSONL) | New — local file in `%LOCALAPPDATA%\HolyHand\audit` |
| Deny-list (per-app) | New — refuse to automate password managers, banking apps |
| Elevated target detection (UIPI) | New — detect and refuse elevated processes |
| Voice input (Whisper.net) | New — upstream has no voice. NAudio + Whisper.net with on-demand model download |
| Vercel AI Gateway integration | New — upstream uses TypeSafe API directly |

---

## 4. Role Mapping: macOS AX → Windows UIA

| macOS AX Role | Windows UIA ControlType |
|---|---|
| AXButton | Button |
| AXMenuItem | MenuItem |
| AXMenuBarItem | MenuItem |
| AXLink | Hyperlink |
| AXTab | TabItem |
| AXCheckBox | CheckBox |
| AXRadioButton | RadioButton |
| AXPopUpButton | ComboBox |
| AXRow | DataItem / ListItem |
| AXCell | DataItem |
| AXDisclosureTriangle | TreeItem / Button |
| AXSwitch | CheckBox (toggle) |
| AXTextField | Edit |
| AXTextArea | Edit / Document |
| AXComboBox | ComboBox |
| AXStaticText | Text |
| AXGroup | Group |
| AXProgressIndicator | ProgressBar |
| AXSlider | Slider |
| AXList | List |
| AXTable | Table / DataGrid |
| AXOutlineRow | TreeItem |
| AXWebArea | Pane (browser content) |
