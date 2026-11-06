---
name: systematic-debugging
description: 4-phase root cause analysis protocol for crashes, binding failures, and network bugs.
---

# Systematic Debugging Protocol

Derived from `obra/superpowers/systematic-debugging`. STRICTLY PROHIBITS "shotgun debugging" (trial-and-error edits) that destabilize working code.

## 1. The 4-Phase Diagnostic Flow

### Step 1: Reproduce (Isolate Reproduction Steps)
- Establish deterministic reproduction steps:
  - Which target platform triggers the bug (Windows Desktop vs Android Emulator)?
  - What exact input data triggers the failure?
  - Create a failing unit test reproducing the bug if it is a logic issue.

### Step 2: Isolate (Localize the Fault Domain)
Determine which architectural layer owns the fault:
- If data fails to render on screen: Is it an **XAML Binding issue (Presentation)**, a **Use Case returning empty data (Core)**, or an **HttpClient network failure (Infra)**?
- Add structured diagnostic logging: `Debug.WriteLine($"[DEBUG] Step X: {value}")`.

### Step 3: Root Cause (Identify the Underlying Flaw)
- Do not merely suppress the symptom; explain **why** the defect occurred.
- Example: Crash on image picker -> Root cause: Missing media permissions in `AndroidManifest.xml` on Android 13+.

### Step 4: Verify & Add Regression Test (Patch & Protect)
- Apply the minimal targeted fix.
- Re-run the reproduction test created in Step 1 -> Verify it passes.
- Execute the full test suite to guarantee no secondary regressions were introduced.
