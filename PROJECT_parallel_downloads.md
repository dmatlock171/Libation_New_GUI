# Project: finish parallel downloads (PR #1885)

Picking up stalled work by **SirBiggin**, addressing the maintainer's review, and
opening a PR that credits him.

- Original PR: https://github.com/rmcrackan/Libation/pull/1885
- Source branch: https://github.com/SirBiggin/Libation-Mulithreaded (branch `master`)
- Opened Jun 2026 · reviewed Jul 2026 · changes requested · author stepped away

## Why this is worth doing

Libation downloads one book at a time. On a large library that is the single biggest
practical limitation, and it is the reason this project started.

The hard part is already done. rmcrackan called the concurrent queue loop and
multi-active `TrackedQueue` "a solid foundation" and requested four specific changes
rather than rejecting the approach. The author then said he had given up on it. So
there is working code, an engaged maintainer with clear requirements, and nobody
doing the work.

Notably, the largest outstanding gap is **Avalonia UI** - which is exactly what this
branch has been working on.

## What SirBiggin already built

- Parallel download and DeDRM via a configurable thread pool
- A `HashSet<Task>` loop that allows concurrency to change mid-run
- `TrackedQueue` reworked from a single `Current` to a list of active items, with
  `Current` retained for backward compatibility
- Auto-scroll keeping active downloads in view (`ScrollToTop` on `VirtualFlowControl`)
- New settings: `MaxConcurrentDownloads`, `AutoScrollQueue`
- A thread-safety fix in `setLiberatedVisibleMenuItem` - `GetVisible()` was being
  enumerated on a background thread while parallel downloads mutated the collection

## The four requested changes

Quoting the review, in the order they should be tackled:

### 1. Installer script (trivial)

> Mind leaving this out of the PR? Hardcoding `C:\Program Files\dotnet\dotnet.exe` is
> machine-specific and unrelated to parallel downloads.

`Scripts/Windows/Build-WindowsInstaller.ps1` - restore the plain `dotnet publish` line.

### 2. Safer defaults

> Defaulting to `ProcessorCount` may trigger Audible license denials on larger
> machines. A small default (e.g. 2-3) and a numeric control would be safer than an
> on/off checkbox alone.

Worth noting for its own sake: this confirms **Audible throttles license requests**.
More concurrency is not linearly faster, and there is a ceiling worth staying under.

### 3. Cancel in-flight downloads

> `ClearQueue()` stops new work, but other active downloads keep running. Could we
> cancel all `Active` items here (same as Cancel All)? Same for the disk-full path.

### 4. Avalonia parity (the main piece)

> Since this lives in shared UI code, Avalonia gets parallel downloads with no control
> to turn them off. Could we default to off (or persist the setting) and add matching
> Avalonia UI?

Two parts: a toggle and numeric limit in Chardonnay's process queue panel, and making
Cancel All in `ProcessQueueControl.axaml.cs` cancel every active item rather than only
`Current`.

## Approach

Work on a branch separate from `gui-status-column`. Mixing the UI work into this would
make the PR unreviewable, and these should be judged independently.

```
git remote add sirbiggin https://github.com/SirBiggin/Libation-Mulithreaded.git
git fetch sirbiggin
git checkout -b parallel-downloads sirbiggin/master
git fetch origin
git rebase origin/master
```

Expect conflicts - `ProcessQueueViewModel.cs` conflicted once already, and upstream has
moved since July. Build before changing anything, so the starting point is known good.

## Credit

His commits carry his authorship and a rebase preserves it, so **do not squash**. The
PR description should say plainly that this continues #1885, link it, and name him as
the original author. He should also be told it is happening - he may want to be
involved, and having walked away from it, being handed a finished version without
notice would be a poor outcome.

## Open questions

1. Does the auto-scroll feature belong in the same PR, or should it be split out? It
   is unrelated to concurrency and could be reviewed separately.
2. Should the concurrency limit live in the process queue panel next to DL Limit, or
   in Settings? The queue panel is more discoverable; Settings is more conventional.
3. Is `2` or `3` the right default? The review says "e.g. 2-3" without committing.
   Worth asking rather than guessing, since he knows the throttling behaviour.
