# Issue draft — formatted for the repo's Bug report template

Where: https://github.com/rmcrackan/Libation/issues/new → choose **Bug report**

**Title field:**

```
Classic: once the process queue panel is collapsed it cannot be reopened, and comes back collapsed on every launch
```

Everything below goes in the body, replacing the template's placeholder text.

Paragraphs are unwrapped on purpose — GitHub turns single newlines into hard breaks.

---

## Describe the bug

In Classic (WinForms), collapsing the process queue panel with the toggle button is a one-way trip. Clicking the toggle again does nothing, and the panel stays collapsed for the rest of the session. The collapsed state is also saved, so it comes back collapsed on every subsequent launch with no way to restore it from the UI.

The only way I found to get the panel back was to edit `Panel2Collapsed` in `Settings.json` by hand while Libation was closed.

Chardonnay is unaffected — it uses a different code path for the same toggle.

## To Reproduce

Steps to reproduce the behavior:

1. Open Libation Classic with the process queue panel visible
2. Click the queue toggle button (`❱❱❱`) to collapse it
3. Click the toggle again to reopen it — nothing happens
4. Close and reopen Libation — the panel is still collapsed
5. `Settings.json` shows `"Panel2Collapsed": true` and never returns to `false`

## Expected behavior

The toggle should reopen the panel, and the open/closed state should persist in whichever state it was left.

## Screenshots

Not applicable — the symptom is a panel that does not reappear.

## Platform

Windows 11. Classic (WinForms) only; Chardonnay is not affected.

## Log Files

Nothing is logged. The affected path is pure UI state and writes no log entries.

---

## Root cause

`Source/LibationWinForms/Form1.ProcessQueue.cs`, in `SetQueueCollapseState` (line numbers from current master).

Collapsing removes the queue control from its panel (line 82):

```csharp
splitContainer1.Panel2.Controls.Remove(processBookQueue1);
splitContainer1.Panel2Collapsed = true;
```

Reopening is then guarded by (line 88):

```csharp
if (!processBookQueue1.PopoutButton.Visible)
    //Queue is in popout mode. Do nothing.
    return;
```

The guard is meant to detect the pop-out window, where `PopoutButton.Visible` is deliberately set to `false` (line 115). But in WinForms `Control.Visible` also returns `false` for a control with no parent — and collapsing has just removed `processBookQueue1` from `Panel2`.

So after a collapse, the guard cannot tell "popped out" from "collapsed". It reads a collapsed queue as popped out, returns early, and never reaches either the reopen code or the `Configuration.Instance.SetNonString(...)` call at the end of the method. The panel stays shut and the saved state stays `true`, which is why it also survives restarts.

## Suggested fix

Track the pop-out state explicitly instead of inferring it from a control property that has a second meaning:

```csharp
private bool queueIsPoppedOut;
```

Set it to `true` in `ProcessBookQueue1_PopOut` and back to `false` in `DockForm_FormClosing`, then use it in the guard:

```csharp
if (queueIsPoppedOut)
    return;
```

I have this working locally and am happy to open a PR if that would help.

## Notes for contributors

- Classic (WinForms) only.
- Self-contained: one method plus two lines of state.
- Reproduces every time.
- Users who hit this end up with a permanently collapsed queue and no obvious way back, since the fix requires hand-editing `Settings.json`.
