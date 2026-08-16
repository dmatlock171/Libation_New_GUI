# Bug report draft: theme editor crashes on second palette colour change

Draft for filing at https://github.com/rmcrackan/Libation/issues

---

## Describe the bug

Changing a palette colour twice in the Chardonnay theme editor crashes Libation with
a fatal error. The first change applies correctly; the second throws.

## To reproduce

1. Settings > Settings... > Important tab > **Edit Theme Colors**
2. Click the swatch for a palette colour (reproduced with `BaseHigh`)
3. Pick a colour — the preview updates correctly, no error
4. Close the colour picker
5. Open the same swatch's colour picker again
6. Libation shows "Libation encountered a fatal error and must close"

### Narrowing

- Opening and closing the picker **without selecting a colour** can be repeated
  indefinitely. No crash.
- The crash requires a colour to have actually been applied first.
- The crash therefore happens on **re-opening the picker after a change**, not during
  the change itself.

This is significant: the exception surfaces during a `Measure` pass (see stack trace),
not during the style swap. The theme rebuild appears to leave the still-open editor's
visual tree in an inconsistent state, and the failure only becomes visible the next
time something in that window re-templates.

## Exception

```
The control Grid already has a visual parent ContentPresenter
(Name = PART_SelectedContentHost) while trying to add it as a child of
ContentPresenter (Name = PART_SelectedContentHost, Host = TabControl
(Name = PART_TabControl)).

at Avalonia.Visual.Avalonia.Collections.IAvaloniaListItemValidator<Avalonia.Visual>.Validate(Visual item)
at Avalonia.Controls.Presenters.ContentPresenter.UpdateChild(Object content)
at Avalonia.Controls.Presenters.ContentPresenter.ApplyTemplate()
at Avalonia.Layout.Layoutable.MeasureCore(Size availableSize)
at Avalonia.Layout.Layoutable.Measure(Size availableSize)
at Avalonia.Layout.Layoutable.MeasureOverride(Size availableSize)
```

## Suspected cause

`Source/LibationAvalonia/Themes/ChardonnayTheme.cs` notes that changes to
`ColorPaletteResources` only take effect by constructing a new `FluentTheme` and
swapping it into `App.Current.Styles`:

```csharp
App.Current.Styles.Remove(oldFluent);
// We must make a new fluent theme and add it to the app for
// the changes to the ColorPaletteResources to take effect.
// Changes to the Libation-specific resources are instant.
```

The theme editor is shown as a window (not a modal dialog) so changes can be
previewed across the app, which means it is still open and templated when the swap
happens. On the second swap, its `TabControl` still holds children realised under the
previous theme, and re-applying templates tries to re-parent a `Grid` that already
has a visual parent.

This is consistent with the symptom that the *first* change succeeds and the second
does not: the first swap happens against a tree templated normally, the second
against a tree templated by the swap itself.

It should also follow that only **palette** colours crash, and Libation-specific
resources do not, since those apply without recreating the theme. Worth confirming.

## Attempted fixes that did NOT work

Both were tried against a local build and the crash still reproduces. Recording them
so nobody repeats the work:

1. **Deferring the swap.** Wrapping the `FluentTheme` rebuild in
   `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)` so it runs after the
   colour picker's event unwinds. No effect — so this is not a mid-event re-entrancy
   problem.
2. **Replacing in place instead of remove-then-add.** Using
   `App.Current.Styles[index] = newFluent` so the app is never momentarily without a
   `FluentTheme`, halving the re-template passes. No effect — so the transient
   theme-less state is not the cause either.

Between them these rule out timing and the remove/add gap. Both still performed the
rebuild while the editor window was open, which — given the narrowing above — is the
part that actually matters. The rebuild damages the open window's visual tree; the
exception then surfaces on the next re-template of that window.

## Possible workaround for users

Untested, but implied by the narrowing: **close the theme editor between colour
changes.** Change one colour, close the editor entirely, reopen it, change the next.
If the damage is confined to the window that was open during the rebuild, a freshly
created window should be clean.

## Suggested direction

Untested, for the maintainer to weigh:

- Rebuild the theme only when the editor **closes** rather than on every colour
  change. Loses live preview, which appears to be the deliberate reason the editor is
  a non-modal window, so this is a real trade-off rather than a free fix.
- Avoid a `TabControl` in the editor, since it is specifically the control that fails.
- Recreate or detach the editor window around the rebuild.

This may also be an Avalonia bug rather than a Libation one — re-applying a theme
under a live `TabControl` arguably shouldn't throw. Worth checking against the
Avalonia issue tracker before working around it.

The same class of problem shows up elsewhere: any control that triggers a theme
change while itself being re-templated will throw this exception. A `ComboBox` bound
to the theme variant crashes identically.

## Environment

- Libation 13.7.7 (Chardonnay), built from source
- Windows, .NET 10
- Reproduced from a clean build with no theme customisations applied beforehand
