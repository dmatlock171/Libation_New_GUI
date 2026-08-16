> **PARTLY STALE.** Anything here about adding an `IsPlusTitle` column or an expiration
> date to the `Book` entity is obsolete: upstream shipped both. See the banner in
> FEATURE_plus_vs_owned.md, and ROADMAP.md for what is actually outstanding.
>
> The 567-unavailable and 94-error figures quoted here came from the old, corrupted
> database. The clean database has 2 errors.

# Tipple — feature notes and backlog

Working notes for the `gui-status-column` branch of Libation.
Not upstream documentation; this is a scratchpad.

---

## Done on this branch

| Change | Files | Notes |
|---|---|---|
| Status vocabulary normalized | `LibationUiBase/GridView/EntryStatus.cs` | Shared code — improves Classic too. Preserves feature parity. |
| Status column header, accessible name | `ProductsDisplay.axaml` | Header `Liberate` → `Status`; `AutomationProperties.Name` for screen readers. |
| "Liberate" → "Download" | `MainVM.BackupCounts.cs`, `MainVM.VisibleBooks.cs`, `MainWindow.axaml` | User-facing strings only. Internal identifiers unchanged. |
| Six menus → four | `MainWindow.axaml` | Library / Download / View / Settings. |
| Grid text sizing (A– A0 A+) | `MainVM.GridFontSize.cs` | Drives existing `GridFontScaleFactor` + `GridScaleFactor` together. |
| Row height only (R– R0 R+) | same | Drives `GridScaleFactor` alone. |
| Ctrl + wheel zoom | `ProductsDisplay.axaml.cs` | Tunnel-routed so it beats the ScrollViewer. |
| Keyboard shortcuts | `MainWindow.axaml` | Ctrl +/-/0, Ctrl+Alt +/- rows, Ctrl+Shift +/- text. |
| Theme menu | `MainVM.Theme.cs` | View → Theme. Deferred write via Dispatcher (see Gotchas). |
| Surname-first author/narrator columns | `LibationUiBase/NameFormatter.cs`, `GridEntry.cs` | Suffix/particle handling, org detection. |
| Imprint filtering | `NameFormatter.cs` | View → Hide imprints. Off by default. |
| Text wrapping | `ProductsDisplay.axaml.cs` | View → Wrap text. Off by default. |
| Title bar | `MainWindow.axaml` | "Libation (Tipple)". |

---

## Known loose ends

Things that are half-done or knowingly inconsistent.

- **macOS `NativeMenu` still has the old six-menu structure.** Only the desktop
  `Menu` was consolidated. Must match before this could go upstream.
- **`Walkthrough.cs` navigates to moved menu items.** The guided tour still
  compiles (all `Name` attributes preserved) but points at items that have moved.
- **New columns don't persist width.** Existing columns bind `Width` two-way to
  `AuthorsWidth`-style view-model properties; the two new ones don't have any.
- **Classic (WinForms) untouched** except where shared code changed. Feature parity
  is a stated project rule.
- **Sort order isn't persisted** across restarts. Pre-existing, not caused by these
  changes. `Configure_ColumnCustomization` is the natural home for it.
- **`Ctrl+Shift` +/- adjusts text without rows**, which is the combination that
  clips. Candidate for removal — it offers nothing `A+` doesn't.

---

## Backlog

### Small, low risk

- **Toolbar "View options" drop-down.** Surface wrap / imprints / theme / reset in
  one toolbar button for discoverability. Must be a `DropDownButton` + `MenuFlyout`,
  **not** a `ComboBox` — see Gotchas.
- **Persist sort order** across restarts.
- **Exit button** in the UI.
- **Drop the `Ctrl+Shift` text-only hotkeys.**
- **Match the WinForms title bar** to "Libation (Tipple)".
- **Persist widths for the two new columns.**

### Medium

- **Excel-style double-click column autofit.** Avalonia's `DataGrid` may not support
  this natively; would likely need a `DoubleTapped` handler setting
  `column.Width = DataGridLength.Auto`. Complicated by columns binding `Width`
  two-way to view-model properties — autofit has to write back, not fight it.
- **Named skins.** Currently there are only two palettes (Light/Dark) with a colour
  editor and JSON import/export. Real named skins would need a themes folder, a
  picker listing its contents, and a setting for the active one. Note: a skin picker
  that re-themes while open will hit the same crash as the ComboBox did.
- **Wire `NameCustomizations.json` into the UI.** The file works but is undiscoverable —
  no UI mentions it, and it isn't created with an example on first run.

### Large / needs maintainer buy-in

- **Rows sized to content instead of fixed height.** *The right fix for several
  problems at once.* Would make wrapping always work, eliminate clipping, remove the
  need for the R buttons and the row-height slider, and make "shrink to fit"
  unnecessary. Risk: cover art and tags columns assume a known row height.
- **Local audiobook import.** See `LOCAL_IMPORT_PROPOSAL.md`. Schema change; serves
  issues #1222, #1086, #1021. Ask on #1222 before building.
- **Full rebrand to Tipple.** Assembly names, product metadata, icon, update-check
  endpoint. Makes upstream merges conflict-prone — hold until the fork genuinely
  diverges.

### Considered and rejected

- **Shrink-to-fit text (`Viewbox`).** Would give every row a different font size,
  which harms scannability, and it fights the A± controls because `Viewbox` applies a
  visual scale on top of the font size. Content-sized rows solve the same problem
  properly. Revisit only if content-sized rows prove infeasible.
- **Store integrations (Chirp, Libro.fm, etc.).** No DRM concern — these stores sell
  DRM-free files — but no public APIs either, so it means scraping authenticated
  sessions, which retailer terms commonly prohibit. Fragile, and every store is new
  code forever. Import solves the same need once.

---

## Gotchas learned the hard way

Worth re-reading before touching related code.

1. **`[PropertyChangeFilter]` does not subscribe anything.** It only filters which
   property a handler reacts to. You must also add
   `Configuration.Instance.PropertyChanged += YourHandler;` in the constructor.
   Forgetting this fails silently — the setting saves, nothing listens.
2. **Never change the theme synchronously from a control's own event handler.** The
   theme swap re-templates every control including the one mid-event, and Avalonia
   throws "already has a visual parent". Use a menu item (closes first) and defer the
   write with `Dispatcher.UIThread.Post`.
3. **`Border` has no `FontSize`.** It isn't a templated control. Use
   `TextElement.FontSize` or set it on something that is.
4. **`JToken.Value<T>()` needs a key argument.** Use `ToString()` or `ToObject<T>()`
   on a `JToken`; `Values<T>()` only exists on `IEnumerable<JToken>`.
5. **Check whether the feature already exists before building it.** Grid font
   scaling, light/dark themes, and the colour editor all already existed and were
   nearly rebuilt from scratch. Grep first.
6. **Line endings.** The clone had 802 phantom modified files from CRLF/LF mismatch.
   Fixed with `core.autocrlf true` + `git rm --cached -r .` + `git reset --hard`.

---

## Dev environment

- Scratch config: `LIBATION_FILES_DIR=C:\Users\dmat\LibationDev` — must be set per
  terminal session, holds a copy of `LibationContext.db` and `Settings.json`.
  `AccountsSettings.json` deliberately **not** copied, so the dev build has no
  Audible account access.
- Config resolution order: `LIBATION_FILES_DIR` env var → `appsettings.json` next to
  the exe → user profile.
- Run: `cd Source\LibationAvalonia; dotnet run`
