# Feature request draft: show which titles are owned vs Audible Plus

Draft for filing at https://github.com/rmcrackan/Libation/issues

---

## Is your feature request related to a problem?

Yes. There is currently no way to tell, inside Libation, whether a title is a
**purchase** or an **Audible Plus / included-with-membership** title.

That distinction matters more in Libation than in the Audible app, because it
determines what will actually download and what will silently disappear:

- Plus titles leave your library when they leave the catalogue, or when membership
  lapses. Purchases don't.
- Downloads that fail for a Plus title fail for a completely different reason than a
  download that fails for a purchase, but the UI presents them identically.
- The `Unavailable` count (books present in the database but no longer returned by
  Audible) is very likely dominated by expired Plus titles - but there is no way to
  confirm that, because the distinction was never recorded.

On a library of a few thousand titles, cross-referencing against the Audible website
one book at a time is not realistic.

## Describe the solution you'd like

Record and display whether each title is a purchase or a Plus title.

1. **A column or badge in the grid.** A small icon in the status area, or a dedicated
   sortable column, showing purchase vs Plus.
2. **Filter support**, so `owned:true` or similar can narrow the grid to purchases.
   This is arguably the most useful part: "download everything I actually own" is a
   common intent that can't currently be expressed.
3. **Expiration date**, where one applies. Libation can already compute it (see
   below), and knowing a Plus title expires next month is directly actionable -
   it tells you what to prioritise.

## Why this looks inexpensive

The data is already flowing through Libation and is being discarded.

`Source/AudibleUtilities/ApiExtended.cs` already filters on Plus status at import:

```csharp
.Where(i => i.IsAyce is not true || Configuration.Instance.ImportPlusTitles);
```

And `Source/AudibleUtilities/Extensions.cs` already computes the expiration date for
Plus titles, with a documented summary describing exactly that purpose:

```csharp
public DateTime? GetExpirationDate()
      => item.Plans
      ?.Where(p => p.IsAyce)
      .Select(p => p.EndDate)
      ...
```

So the API supplies it, and Libation already reads it - it just isn't persisted.
A search of `Source/DataLayer` finds no reference to `IsAyce`, `Ayce`, or any
expiration field, so the information is lost as soon as import finishes.

## What would need to change

1. Two fields on `Book`: a flag for Plus/AYCE, and a nullable expiration date.
   This is a schema change and needs an EF migration.
2. Populate them in the import path, where `IsAyce` and `GetExpirationDate()` are
   already in scope.
3. Surface them: a grid column or badge, and a search field so they can be filtered.

Step 1 is the only part with real risk. Steps 2 and 3 are small.

Existing rows would default to unknown rather than being wrongly labelled as
purchases - a migration can't recover information that was never stored, and
mislabelling a Plus title as owned would be worse than showing nothing.

## Alternatives considered

- **Inferring from failed downloads.** Unreliable, and only tells you after the fact.
- **Cross-referencing the Audible website.** Not practical at scale.
- **Using the `Unavailable` flag as a proxy.** It conflates expired Plus titles with
  titles missing for other reasons, which is precisely the ambiguity this would fix.

## Open questions

1. Would you prefer this as a grid column, a badge on the existing status button, or
   both?
2. Should `ImportPlusTitles` remain the only Plus-related setting, or would a
   "download purchases only" option be worth having alongside it?
3. Is a schema change acceptable here, or would you rather this information were
   derived at scan time and held in memory only?
