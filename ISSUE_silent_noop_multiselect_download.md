# Issue draft — formatted for the repo's Bug report template

Where: https://github.com/rmcrackan/Libation/issues/new → choose **Bug report**

**Title field:**

```
Multi-select Download silently does nothing when all selected books are already downloaded
```

Everything below goes in the body, replacing the template's placeholder text. The
template's headings are kept so it lines up with what rmcrackan expects.

Paragraphs are unwrapped on purpose — GitHub turns single newlines into hard breaks.

---

## Describe the bug

Selecting several already-downloaded books and choosing Download produces no response at all: no queue activity, no dialog, no status message, and nothing in the log file. It reads as a broken button or a selection that didn't register. Libation has in fact done exactly what was asked and found nothing to do, but never says so.

The single-book path in the same method is noticeably more forthcoming — it logs a warning, and shows an explanatory dialog when the audio file is missing. Only the multi-select path is silent.

## To Reproduce

Steps to reproduce the behavior:

1. Select two or more books that are already downloaded (green/liberated)
2. Click Download (or Download and split by chapter)
3. Nothing happens — no queue, no message, no log entry

## Expected behavior

Some acknowledgement that the request was understood and skipped. A short message such as "All 3 selected titles are already downloaded" would do, or at minimum a log entry so it can be diagnosed after the fact.

## Screenshots

Not applicable — the defining symptom is that nothing appears on screen.

## Platform

Windows 11. Not platform specific: the code involved is in `LibationUiBase`, shared by Classic and Chardonnay.

## Log Files

Nothing is logged when this occurs, which is part of the problem. The multi-select branch returns without writing any entry, so the log is silent for the whole interaction.

---

## Root cause

`Source/LibationUiBase/ProcessQueue/ProcessQueueViewModel.cs`, in `QueueDownloadDecryptAsync` (around line 216 on current master):

```csharp
var toLiberate = libraryBooks.UnLiberated().ToArray();

if (toLiberate.Length > 0)
{
    // ...queue the books...
    return true;
}
// falls through
return false;
```

When every selected book is already liberated, `toLiberate` is empty, the block is skipped, and the method returns `false` having neither queued anything nor reported anything.

Compare the single-book path just above:

```csharp
Serilog.Log.Logger.Warning(
    "Download not queued: single-item backup not applicable for {libraryBook} (book status or type does not request download).",
    item.LogFriendly());
if (!item.Book.AudioExists)
{
    await MessageBoxBase.Show(
        "Libation could not queue a download for this title.\n\n"
        + "If it should be downloadable: confirm it is not already liberated, try \"Set download status\" to Not downloaded, or check whether a library scan is required.",
        ...);
}
```

Two paths through one method, giving very different feedback for what is, to the user, the same situation.

## Suggested fix

Handle the empty case explicitly in the multi-book branch instead of falling through. A log line is the minimum; a brief message is better, and could reuse the single-book wording so the guidance stays consistent.

One judgement call for whoever takes this: whether an empty result should stay quiet for large selections. "Download All" against an already-complete library would pop a modal every time, which is worse than silence. A status-bar message may fit that case better than a dialog.

## Notes for contributors

- Affects both Classic and Chardonnay; the logic is in shared UI code, not in either UI project.
- Self-contained: one method, no threading, no API calls.
- Reproduces every time.
- Suitable as a first contribution, though picking the right feedback for the bulk case takes a little thought.
