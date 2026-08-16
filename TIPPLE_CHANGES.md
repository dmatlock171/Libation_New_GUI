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
A row of icon-and-label buttons between the menu bar and the filter toolbar, grouped into captioned
sections the way Word does: **Library** (Scan), **Download** (ALL, Books, PDFs), **Filter** (At Risk
and your saved quick filters), **Tools** (Export, Settings). Live counts appear as tooltips.

The ribbon shares one row with the filter box: sections on the left at their natural width, the
filter stretching through the middle, the grid size steppers on the right. **View > Show Ribbon**
(`ShowRibbon`, on by default) hides the button sections; the filter box stays, since the row exists
for it either way. The first ten saved quick filters appear as buttons in the
Filter section; they sit last so a narrow window clips them rather than Scan or Download.

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
  - Each row of buttons shows the size currently in effect — text size in px beside A, row
    height in px beside R — so the controls are not two pairs of unlabelled arrows.
  - Sizes step in round numbers: 0.5px for text, 5px for rows. Upstream steps the underlying
    scale factor by 0.1, which lands on sizes like 12.1px. A value left on an odd number by an
    older build or by the Settings slider snaps to the next round one on the first press.
  - The two are fully decoupled, which means text large enough to outgrow its row will be
    clipped; the fix is to press R+. Sizing rows automatically to fit the text was tried and
    removed: it left A still moving the rows, and left R doing nothing at all whenever the font
    scale happened to be ahead of it.
- **Text wrapping** in columns too narrow to fit their contents (`GridTextWrapping`, off by
  default), toggled from the wrap-symbol button beside the A and R controls as well as from the
  View menu. Wrapped text still needs somewhere to go, so it pairs with R+ when rows are too short
  for the extra lines.
- **Surname-first author and narrator columns**, so sorting by author behaves the way a bookshelf does.
- **Alternating row shading** in the grid and the download queue, adjustable from the **S**
  steppers beside A and R and stored per theme as `AlternatingRowBackgroundBrush`.
- **Imprint filtering** (`StripNonPersonAuthors`, off by default) hides brands such as
  "The Great Courses" from the surname columns, where they sort as though they were people.

---

## At risk

A ribbon button that filters to **every Audible Plus title you have not downloaded**. Plus titles
leave the catalogue on Audible's schedule, and until now nothing in the UI said so beforehand:
they simply stopped being in the library.

This library had already lost 95 books that way. Every one was Plus, every one had never been
downloaded, and not a single owned title has ever vanished.

**Why it does not filter on the expiry date.** It did at first, showing only titles expiring within
90 days. The data disproved that: of those 95 lost titles, *none* carried an expiry date. Only 143
of 2,194 Plus titles have one at all, and those have never been the ones that disappear. A missing
date does not mean safe — it means Audible has not published a deadline, which is both the normal
case and the one that costs you books. Dates are used for sorting instead, so anything with a known
deadline can be brought to the top by sorting on Included Until.

`IncludedUntil` is indexed for search regardless, so date queries are available by hand as
`includeduntil:[20260101 TO 20260401]`, with `Expires` and `Expiry` as aliases. Adding a field to
the index also made the index self-healing: it carries a fingerprint of the indexed field names and
rebuilds when that changes, because Lucene answers a query about a field it has never seen with
silence rather than an error.

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

Fuller detail, with reasoning and priorities, is in ROADMAP.md.

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

### Antivirus false positives on your own build

A locally built executable is unsigned and has no reputation with the big antivirus vendors, and
heuristics like Norton's SONAR and Download Insight flag on exactly that, independent of what the
code does. Official Libation releases are signed, which is why they do not trip it. Expect it to
recur after every rebuild, because each build produces a new hash. No code change fixes this.

**Norton.** The notification that appears has no "allow" option because the file has already been
removed by the time you see it. To recover the current build: Norton → **Security** → **History**,
set the **Show** dropdown to **Quarantine**, select the item, **Options**, then **Create exception
& restore** (older versions: **Restore & exclude this file**).

To stop it recurring, note that Norton keeps *two* separate exclusion lists and adding to only the
first will not help. Under Settings → **Antivirus** → **Scans and Risks**, add the build output to
both **Items to Exclude from Scans** and **Items to Exclude from Auto-Protect, SONAR and Download
Intelligence Detection**:

```
<repo>\Source\bin
<repo>\Source\obj
```

Exclude the folders rather than individual files — a file exclusion is keyed to a hash that the
next build invalidates. If the quarantine list is empty and the file is instead blocked at launch,
that is Download Insight rather than Auto-Protect, and the second list is the one that governs it.

Excluded folders are not scanned at all, so keep this scoped to the build output rather than the
repository root or your user profile.
