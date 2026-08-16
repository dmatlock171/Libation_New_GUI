# Proposal: import audiobooks Libation didn't download

**Status:** draft for discussion — not implemented
**Related issues:** #1222 (Libro.fm), #1086 (Libro.fm scraping fork), #1021 (other sources)

## Summary

Let users add audiobooks they already own to the Libation library, regardless of
where those files came from. No store integrations, no credentials, no scraping.

This is deliberately *not* "add support for Chirp / Libro.fm / Downpour." It is one
feature that serves all of them, plus LibriVox, ripped CDs, and files inherited from
a previous audiobook manager.

## Motivation

Three open issues ask for non-Audible sources. The request in #1222 describes the
current workaround plainly: download from the other store by hand, then rename files
and folders to match the Libation naming template, so the files at least live in the
right place. The books still don't appear in the grid, so Libation stops being the
one place to see what you own.

That is the actual complaint. It is a *library* problem, not a *download* problem.
Users are not asking Libation to break anything or to log into anything — they are
asking for their shelf to be complete.

## Why not store integrations

Worth stating explicitly, because it is the obvious first idea:

- **No DRM concern.** Chirp, Libro.fm and Downpour all sell DRM-free files. There is
  nothing to decrypt. Whatever value Libation adds for Audible does not apply here.
- **Terms of service, not copyright, is the risk.** None of these stores publish an
  API. Integration means driving an authenticated session programmatically, which
  retailer terms commonly prohibit even for content the user has bought. That is a
  contract question and it does not go away just because the files are unencrypted.
- **Ongoing maintenance.** A scraper breaks whenever the store's markup changes, and
  one that handles store credentials is a support burden for a solo maintainer.
- **It scales badly.** Every new store is new code, forever.

Import has none of these properties, and covers every store at once.

## Current constraints

The data model assumes Audible throughout:

- `Book.AudibleProductId` is required, immutable, and validated non-empty.
- `LibraryBook` associates every entry with an Audible `Account`.
- The library scanner, the "scan for better quality" feature, and the
  "locate audiobooks" matcher all key off the Audible product ID.

So imported books cannot simply be inserted. Either they get fabricated Audible IDs —
which will confuse all three of the features above — or the model grows an explicit
notion of where a book came from. The second is the honest option.

## Proposed design

### 1. Give books a source

Add a source discriminator to `Book`, defaulting to `Audible` so every existing row
migrates unchanged. Introduce a source-neutral identifier; for Audible entries it
continues to be the ASIN, for imported entries it can be a stable hash of the file
path or the embedded metadata.

This is a schema change and needs an EF migration. It is the load-bearing decision in
this proposal and the part most worth agreeing on before any code is written.

### 2. Make the Audible account optional

`LibraryBook` should tolerate a null account for entries whose source isn't Audible.
Anything that dereferences the account needs an audit — this is likely the largest
share of the work and the easiest place to introduce null-reference bugs.

### 3. Teach Audible-specific features to skip non-Audible entries

Library scan, better-quality scan, and re-download must filter to Audible-sourced
books rather than assuming every row has an ASIN. Failing to do this produces
confusing errors rather than clean no-ops.

### 4. Import UI

Point Libation at a folder. For each audio file or folder found:

- Read embedded tags for title, author, narrator, series, length, cover art.
- Show a preview list with a chance to correct obviously wrong metadata before commit.
- Create library entries already marked as downloaded, pointing at the existing files.

Import should never move, rename, or rewrite the source files. Users have organised
these deliberately; touching them would be a serious breach of expectations.

### 5. Grid behaviour

Imported entries show a distinct status — "Imported" or "Local" — rather than a
download state. The download action is a no-op for them. Sorting, filtering, tags,
and export all work as normal, which is the entire point.

## Scope boundaries

Explicitly out of scope:

- Any store integration, credential handling, or scraping.
- Any DRM handling for any platform.
- Watching folders for changes. A manual re-scan is enough for a first version.
- Deduplicating an imported book against an Audible copy of the same title. Worth
  doing eventually; not needed to make this useful.

## Open questions for the maintainer

1. **Is this a direction you want at all?** It changes Libation from an Audible
   liberation tool into a general audiobook library. That is a product decision, and
   it should be settled before anyone writes a migration.
2. **Is a schema change acceptable** for this, or would you prefer imported books
   live outside the existing `Book` table entirely?
3. **Chardonnay only, or parity with Classic?** The stated rule is that both UIs have
   identical features, which roughly doubles the UI work.
4. **How much metadata correction belongs in the import flow** versus being left to
   the existing tag-editing features?

## Suggested sequencing

The schema and account-nullability work (steps 1–3) is the risky part and delivers
nothing visible on its own. The import UI (steps 4–5) is comparatively
straightforward once the model allows it.

A reasonable first slice: schema change plus import of a single folder, with no
preview or correction UI, behind a setting that defaults to off. That is enough to
validate the model without committing to the full feature.
