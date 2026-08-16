# Tipple roadmap

What's worth doing next and why, ordered by value rather than by when it was thought of.

`TIPPLE_CHANGES.md` describes what the fork already does. This file is what it doesn't.

Last reviewed against the live library on 2026-08-16: 2,645 books, 2,194 Audible Plus, 451 owned.

---

## Blocking

### Build and verify the UI work
A long run of Chardonnay changes went in without a compile between them. The two most likely to
break are `MainVM.RowShade.cs` — `App.Current.Resources.ThemeDictionaries[variant]` cast to
`ResourceDictionary`, and `ChardonnayThemePersister.Create()?.Target` — and the restructured
ribbon block in `MainWindow.axaml`.

Worth checking by eye once it runs:

- A / R / S steppers all visible, readouts aligned, values sensible
- Ribbon sections captioned Library · Download · Filter · Tools, icons the same height
- **View → Show Ribbon** hides and restores the strip
- Row shading visible in both themes, including the Status column, and **persisting across a
  restart** — that one has been wrong twice
- **At Risk** returns rows. The search index self-rebuilds on the first search after the field
  set changed, so the first query after launch will pause while ~2,600 books re-index.

---

## Features

### 1. Learn whether Audible publishes the missing expiry dates
**Answered in part, and the answer changed the feature.**

Of 2,194 Plus titles only 143 carry an `IncludedUntil` date. The assumption was that this was a
gap to close. Then the library settled it: **of the 95 titles already lost, none had an expiry
date — not one.** The 143 dated titles have never been the ones that vanish.

So a missing date does not mean safe; it is the normal case and evidently the dangerous one. At
Risk no longer requires a date and instead lists every undownloaded Plus title, using dates only
for ordering.

What is still unknown is *why* they are missing. `GetExpirationDate()`
(`AudibleUtilities\Extensions.cs:17`) discards three things, all of which become an
indistinguishable `null`:

- `EndDate` years 2099 and 9999, Audible's "indefinite" sentinels
- any `EndDate` already in the past
- anything when `Plans` is absent or has no AYCE entry

The main import path does request `ProductPlans` (`LibraryCommands.cs:130`), so the data is being
asked for. Worth logging the raw `Plans` array for a sample of titles during one scan to find out
which of the three cases dominates. If most undated titles are really 2099 sentinels, storing that
as "indefinite" rather than `null` would at least distinguish "no deadline announced" from "we
have no data" — and would tell the user which is which.

Note `importSingleToDb` (`LibraryCommands.cs:244`) is fed by a scan configured with only
`ProductAttrs | ProductDesc | Relationships` (`LibraryCommands.cs:54`), so titles imported through
that path get no plan data at all. Worth confirming whether that path can write a null over a good
value.

### 2. Expiring-soon count and startup warning
Finishes At Risk: a count in the status bar, and a nudge at startup when something is close.

Hook `OnLibraryLoadedAsync` rather than `MainWindow_Opened` — the latter fires before the library
is loaded, so a data-dependent check there sees nothing. `LibraryCommands.GetCounts` already
carries the whole `libraryBooks` enumerable into `LibraryStats`, which is the natural place to add
a count. Follow the `MessageBox.*_ShowIfTrue()` shape for the warning.

### 3. Smarter Locate Audiobooks
Matching only succeeds when the Audible ASIN appears in the file or folder name, which most naming
templates omit. Existing files go unrecognised and risk being downloaded again — potentially
hundreds of GB. Wants fuzzy title and author matching with a confirm-before-apply review list.

Largest scope of the three, and the biggest practical win for anyone with an existing collection.

### 4. Local audiobook import
Bringing non-Audible files into the library. Still the least-designed item: identifier scheme
(GUID rather than a path or metadata hash), nullable `Account`, multi-file versus single-file
books, missing-file handling, cover art. See `LOCAL_IMPORT_PROPOSAL.md`.

### 5. Error surface and failure reasons
**Deprioritised.** This was motivated by 94 errors that turned out to be database rot; the clean
database has 2. A per-book failure-reason column is still nice, but it is no longer solving a
problem this library has.

---

## Upstream

| Item | State |
|---|---|
| **PR #1885**, parallel downloads | All four review points addressed and rebased. Comment posted 2026-08-16 asking SirBiggin whether to keep it as his PR or open a separate one. If no reply, open a separate PR crediting him — nothing on this fork depends on him. |
| **`LogPathValidator`** | The cleanest standalone PR available: self-contained, fixes a real gap upstream, no dependency on any other Tipple work. |
| Theme editor crash | Filed as issue #1940 with a fix. |
| *Classic* queue panel stuck closed | Fixed locally, draft written, **not filed**. |
| Scroll bars collapsing to a sliver | Fixed locally, **not filed**. |
| Silent no-op multi-select download | Draft written, **not filed**. |

---

## Smaller things

- **Ribbon icon size adjuster.** The icons are pinned to 18px tall by a style setter in
  `MainWindow.axaml`. Worth exposing as a setting, and possibly as a fourth stepper alongside
  A / R / S, so the ribbon can be scaled for high-DPI displays or shrunk when the vertical space
  matters more than the affordance. Only the height needs to move — width follows the artwork
  now — so it is the same shape of change as the S stepper, minus the theme plumbing.
- `RibbonQuickFilterCount` is 10. Drop to 5 if the ribbon crowds on a narrow window.
- The Status button is flatter now that its disabled state no longer paints a background. If that
  reads as too flat, add a border rather than restoring the fill.
- `AlternatingRowBackgroundBrush` is adjustable from the S steppers and the theme editor. Defaults
  are `#14000000` light and `#26FFFFFF` dark.

---

## Settled — do not reopen

**Owned vs Audible Plus is already implemented upstream.** Migrations
`20251020175053_AddIncludedUntil` and `20260107224303_AddIsAudiblePlus` are applied, the data is
populated, and the UI exists: a sortable **Included Until** grid column (hidden by default), a
searchable `Plus` / `AudiblePlus` field, context-menu and queue-status support.

`FEATURE_plus_vs_owned.md` proposed building all of this and is obsolete. The proposal was written
against an older Libation; only the database showed that upstream had moved. Anything in
`FEATURE_NOTES.md` about adding `IsPlusTitle` or an expiration column is stale for the same reason.

The standing rule that produced this — **grep before building** — has now paid for itself three
times: grid font scaling, the colour editor, and this. The lesson from this one is broader: check
the *data*, not just the source, before trusting a backlog note.
