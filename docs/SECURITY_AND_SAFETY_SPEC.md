# HolyHand Safety, Security & Human-in-the-Loop Specification

> **Document**: Safety Invariants & Risk Policy Specification  
> **Status**: Non-Negotiable Core Invariants  
> **Target**: `HolyHand.Core.Safety` & `HolyHand.App.Windows`

---

## 1. Core Principles

HolyHand suffered from a severe safety regression where "Jarvis Mode" hollowed out all confirmation dialogs, allowing automated submission of financial payments, job applications, and license purchases without user awareness.

**HolyHand permanently restores and hardcodes the Human-in-the-Loop Safety Contract:**
1. **Risk decisions are made in plain, deterministic code first** — not by an AI model.
2. **Irreversible or external-effect actions require explicit human confirmation** before physical execution.
3. **The AI model can escalate risk, but can NEVER bypass code-mandated confirmations.**
4. **Untrusted screen text is treated strictly as data, never as instructions.**
5. **Execution must be strictly bounded** by step caps and loop guards to prevent infinite runaway loops.

---

## 2. Action Risk Taxonomy

Every proposed action is classified into one of three risk tiers before execution:

```
┌─────────────────────────────────────────────────────────────┐
│                 Proposed Action Evaluation                  │
└──────────────────────────────┬──────────────────────────────┘
                               │
               ┌───────────────┴───────────────┐
               ▼                               ▼
       Prohibited Action?              Sensitive Action?
   (Regex matches deletion)       (Matches Irreversible Verb)
               │                               │
       YES: ABORT IMMEDIATELY          YES: PAUSE & SHOW MODAL
               │                               │
       NO: Continue check              NO: AUTO-EXECUTE (Harmless)
```

### Tier 1: Prohibited Actions (Hard Abort)
Actions targeting data destruction or system wiping are unconditionally blocked. If detected in user goal, target button, or typed text, HolyHand terminates the run immediately:
* **Prohibited Terms**: `delete`, `deletion`, `erase`, `wipe`, `destroy`, `truncate`, `format`, `del`, `rmdir`, `drop table`.

### Tier 2: Irreversible Actions (Mandatory Human Confirmation)
Actions with external side-effects, legal/financial commitments, or irreversible state changes pause the agent and display the **Confirmation Modal**. The action **does not execute** until the user explicitly presses Enter or clicks Approve.
* **Sensitive Verbs**:
  - **Financial**: `Pay`, `Buy`, `Purchase`, `Order`, `Checkout`, `Transfer`, `Subscribe`, `Tip`
  - **Submission**: `Submit`, `Apply`, `Send`, `Post`, `Publish`, `Confirm`, `File`, `Register`
  - **System**: `Install`, `Run`, `Uninstall`, `Overwrite`, `Reboot`, `Shutdown`

### Tier 3: Routine / Harmless Actions (Auto-Executed)
Safe actions that are reversible or exploratory execute automatically with zero user friction:
* Navigational clicks (tabs, accordions, links, menu items).
* Typing into search boxes, filter inputs, text editors.
* Scrolling, pressing Tab, pressing Space (play/pause).

---

## 3. Human Confirmation Modal Contract

When an action triggers Tier 2 risk, the **WPF ConfirmationDialog** appears centered on screen:

```
┌──────────────────────────────────────────────────────────────┐
│  ⚠ HUMAN APPROVAL REQUIRED                                  │
│  A sensitive action was paused for your safety.              │
├──────────────────────────────────────────────────────────────┤
│  Action:   Click Button                                      │
│  Target:   "Submit Application"                              │
│  App:      chrome.exe ("Careers - Senior Engineer")          │
│  Reason:   Action matches sensitive verb 'Submit'            │
├──────────────────────────────────────────────────────────────┤
│  [ Reject & Abort (Esc) ]        [ Approve & Execute (Enter) ]│
└──────────────────────────────────────────────────────────────┘
```

### Modal Invariants:
1. **Clear Attribution**: States the exact action type, target label, application executable, and window title.
2. **Single-Stroke Decision**: Pressing `Enter` approves and resumes. Pressing `Esc` immediately aborts the run, executes nothing, and restores the target window.
3. **Fail-Closed**: If the modal loses focus, times out, or encounters an internal exception, the action is **rejected by default**.

---

## 4. Execution Bounds & Guardrails

### 4.1 Strict Step Limit (`MaxSteps`)
* **Default**: `25` steps per run (configurable via `MAX_STEPS_PER_RUN`).
* Eliminates the risk of infinite runaway typing or clicking loops.

### 4.2 State-Hashing Loop Guard (`LoopGuard`)
* Computes a SHA-256 signature of the visible screen elements (`Id | Role | Label | Value | Enabled | Focused`).
* If the screen state signature remains identical across **3 consecutive actions**, the agent enters stall recovery:
  1. Pauses for $1500\text{ms}$ to allow page/DOM rendering.
  2. Re-reads the screen.
  3. If still unchanged after 5 consecutive observations, halts the task with status `Stalled` and consults the user.

### 4.3 Probabilistic Confidence Floor
* When Jev returns candidate probabilities for `nextAction`:
  - If `topChoice.Probability >= 0.55`: Proceed with execution.
  - If `topChoice.Probability < 0.55`: The agent halts, notifies the user of ambiguity, and transitions to `AskUser` mode.

---

## 5. Privacy, Redaction & Secrets

### 5.1 On-Screen Data Redaction (`SecretSanitizer`)
Before any element label or value is logged to disk or sent to the Jev model:
1. **Password Controls**: Any element with `IsPassword == true` is scrubbed to `[PASSWORD]`.
2. **Credit Cards**: Luhn-matching 13–16 digit numbers are replaced with `[REDACTED_CARD]`.
3. **API Keys & Tokens**: Patterns matching `vck_*`, `sk-*`, `ghp_*`, and JWT strings are replaced with `[REDACTED_KEY]`.
4. **Bearer Headers**: Authorization strings are replaced with `Bearer [REDACTED]`.

### 5.2 Application Deny-List
HolyHand strictly refuses to interact with or read elements from sensitive applications:
* Password Managers: `1password`, `bitwarden`, `keepass`, `keepassxc`, `lastpass`, `dashlane`, `enpass`.
* Windows Security: `credentialmanager`, `authenticator`.
* Administrative Shells: `regedit`, `cmd`, `powershell`, `diskpart`.

### 5.3 UIPI Privilege Isolation
Windows UIPI prevents lower-privilege applications from interacting with elevated (Admin) windows.
* HolyHand inspects the foreground process token via Win32 `OpenProcessToken`.
* If the target is elevated and HolyHand is non-elevated, HolyHand displays an **"ELEVATED TARGET"** warning badge and safely refuses to attempt clicks that Windows would drop.
