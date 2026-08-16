# Tipple — what's different from Libation

Tipple is a personal fork of [Libation](https://github.com/rmcrackan/Libation), Rob McCrackan's
open-source Audible library manager. It is not a competing project and not a rebrand: it tracks
upstream, uses the same database and settings files, and most of what is listed here is either
already proposed upstream or intended to be.

Everything below applies to **Chardonnay**, Libation's Avalonia UI, unless a change is marked
*Classic* (the WinForms UI). The window title says "Tipple" so it is obvious which build is
running when both are installed.

**Why the fork exists.** Two things, mostly. Libation downloads one book at a time, which is slow
for a large library. And when something goes wrong it tends to go wrong *quietly* — a
misconfigured log path or an ignored environment variable produces no message anywhere in the UI.
Several changes here are aimed squarely at that second problem.

---

## Downloads

### Parallel downloads
Libation downloads and decrypts one book at a time. Tipple downloads several at once, with a
**Max concurrent downloads** setting (default 3) and an "At once" control in the queue panel.

The work originates from [PR #1885](https://github.com/rmcrackan/Libation/pull/1885) by
**SirBiggin**, rebased here with his commit authorship preserved, plus the changes the maintainer
asked for:

- Chardonnay parity — the toggle and numeric limit exist in the Avalonia queue panel, not just Classic.
- **Cancel All** cancels every active item, not only the one at the head of the queue.
- Aborting, and running out of disk space, now cancel downloads that are already in flight.
- The default is 3 rather than one-per-CPU-core. Higher values risk Audible returning
  `Content License denied`; this was reproduced in testing, so 3 is a deliberate ceiling on the
  default rather than a guess.
- An unrelated installer change from the original PR was dropped.

**Why 3 and not more.** Audible appears to throttle license requests. Pushing concurrency higher
produced license denials that look like ordinary download failures, which is a worse experience
than a slower queue.

### Queue behaviour
- Free slots are filled when books are queued *after* the download loop has already started,
  instead of waiting for the current batch to drain.
- **Auto-scroll** keeps active downloads in view (`AutoScrollQueue`, on by default).

---

## Main window

### Ribbon toolbar
A row of icon-and-label buttons between the menu bar and the filter toolbar, covering the actions
that are otherwise buried in menus: Scan, Download ALL, Download Books, Download PDFs, Export,
Settings. Live counts appear as tooltips.

**Why.** The most-used actions in a library manager are "scan" and "download", and both were two
levels into a menu.

### "Liberate" renamed to "Download"
Upstream's term for downloading and decrypting is *Liberate*. Tipple calls it Download in the UI.

**Why.** It is the only word in the interface that has to be learned before the app makes sense.

### Menu consolidation
Menus were regrouped, and Settings gained a **Diagnostics...** entry (see below).

---

## Library grid

- **Status column** showing each book's download state directly in the grid.
- **Sort order and column widths persist** across restarts (`GridSortColumn`, `GridSortDescending`).
- **Text size (A) and row height (R) controls** in the toolbar, plus `Ctrl`+`+`/`-` and
  `Ctrl`+mouse wheel to scale both at once.
  - **A** changes text size and nothing else. **R** changes row height and nothing else.
  - The two are fully decoupled, which means text large enough to outgrow its row will be
    clipped; the fix is to press R+. Sizing rows automatically to fit the text was tried and
    removed: it left A still moving the rows, and left R doing nothing at all whenever the font
    scale happened to be ahead of it.
- **Text wrapping** in columns too narrow to fit their contents (`GridTextWrapping`, off by default).
- **Surname-first author and narrator columns**, so sorting by author behaves the way a bookshelf does.
- **Imprint filtering** (`StripNonPersonAuthors`, off by default) hides brands such as
  "The Great Courses" from the surname columns, where they sort as though they were people.

---

## Download queue panel

- **Resizable and collapsible**, with the width remembered. Upstream used a `SplitView` with a
  fixed pane length and no resize grip, so the queue could not be resized at all.
- **Bottom bar reflowed onto two rows.** Cancel All, Clear Finished, Auto-scroll, "At once" and
  "DL Limit" need roughly 560px side by side, which pushed Clear Finished off the right edge of a
  normally-sized queue pane.
- **Scroll bars no longer collapse to a sliver.** The Fluent theme scales the thumb via a render
  transform gated on hover; the fix pins `ScrollBarSize` instead of setting `Width`.
- *Classic*: the queue panel could get **stuck closed** with no way to reopen it short of
  hand-editing `Panel2Collapsed` in `Settings.json`. Fixed, and its width now persists too.
- *Classic*: fixed overlapping controls in the process queue panel.

---

## Themes

- A **theme menu** and a library of **named colour schemes**.
- The theme editor gained a **description column** and **`Ctrl`+`Z` undo**.
- **Fixed a crash in the theme editor** present in shipped Libation, caused by rebuilding the
  Fluent theme on every individual colour change. The rebuild is now batched until the editor
  closes. Filed upstream as [issue #1940](https://github.com/rmcrackan/Libation/issues/1940).
- Window outlines and title-bar tinting so the window chrome follows the selected theme.

---

## Diagnostics and failure visibility

This section is the reason the fork exists as much as parallel downloads are.

### Settings → Diagnostics
A read-only dialog showing where everything actually is and whether it works: version and OS,
the resolved Libation files directory, `appsettings.json` and `Settings.json`, the database path
and size, book and trash counts, and the logging configuration. Problems are highlighted rather
than left to be inferred, and **Copy to Clipboard** produces plain text suitable for a bug report.

Two checks in particular:

- **`LIBATION_FILES_DIR` set to a directory that does not exist.** Libation silently ignores the
  variable in that case and falls back to `appsettings.json`. The symptom is an empty library and
  no explanation. This is now reported as an error that says exactly that.
- **An unwritable log destination** — see below.

PostgreSQL connection strings are deliberately not displayed, since the dialog has a clipboard
button and the string contains credentials.

### Startup warning when logging is not working
Libation validates the *shape* of the Serilog configuration at startup — that `WriteTo` exists,
that each sink has a name, that `MinimumLevel` parses. It does not check that the log destination
is reachable, and Serilog's file sink does not throw when it cannot open a file. A structurally
perfect configuration pointing at a drive letter that no longer exists therefore starts cleanly
and logs nothing, indefinitely.

Tipple checks the configured path at startup and warns the user if the folder is missing, is not
writable (tested by writing, since permissions and dead network shares both pass an existence
check), or if no log file was opened. Both UIs show it.

The warning is not suppressible and has no "don't show again" option, because the entire failure
mode is that the problem is already invisible. It stops appearing once the path is fixed.

---

## Settings added

| Setting | Default | What it does |
|---|---|---|
| `MaxConcurrentDownloads` | 3 | Books downloaded and decrypted at once (1–10) |
| `AutoScrollQueue` | on | Keep active downloads scrolled into view |
| `GridTextWrapping` | off | Wrap text in columns too narrow to fit |
| `StripNonPersonAuthors` | off | Hide imprints and brands from the surname columns |
| `GridSortColumn` / `GridSortDescending` | — | Remembers the last grid sort |

Existing settings are untouched, so a Tipple install can be pointed back at stock Libation without
migrating anything.

---

## Bugs fixed that belong upstream

| Bug | Status |
|---|---|
| Theme editor crash on colour change | Filed as [#1940](https://github.com/rmcrackan/Libation/issues/1940) with a fix |
| Parallel downloads review changes | On [PR #1885](https://github.com/rmcrackan/Libation/pull/1885) |
| *Classic* queue panel stuck closed | Fixed locally, not yet filed |
| Serilog path never checked for reachability | Fixed locally, not yet filed |
| Scroll bars collapsing to a sliver | Fixed locally, not yet filed |
| Thread-safety crash in `setLiberatedVisibleMenuItem` during parallel downloads | Fixed locally |

---

## Not done yet

- **Owned vs Audible Plus.** Libation reads `IsAyce` and computes an expiration date at import,
  then discards both. Without them there is no way to tell which downloads failed because the
  title left Plus rather than because something broke. Needs a schema change and an EF migration.
- **Local audiobook import** — bringing non-Audible files into the library. Largest open design
  question in the fork.
- **Smarter "Locate Audiobooks."** Matching only works when the Audible ASIN appears in the file
  or folder name, which most naming templates omit, so existing files go unrecognised and risk
  being downloaded again.

---

## Building and running

Requires the .NET 10 SDK. The Avalonia UI uses Avalonia 12.

```powershell
# from the repo root
.\run-dev.ps1                          # uses E:\_LibationClean
.\run-dev.ps1 -FilesDir <path>         # or point it somewhere else
```

`run-dev.ps1` sets `LIBATION_FILES_DIR` for that process only, so a development build can be run
against a scratch library without touching the real one. Note that the variable is ignored if the
directory does not exist — Settings → Diagnostics will say so if that happens.

The full solution is `Source/Libation.slnx`. Classic (WinForms) and Chardonnay (Avalonia) share
the core projects, so changes below the UI layer affect both.
