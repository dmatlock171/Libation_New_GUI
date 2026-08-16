namespace DataLayer;

/// <summary>
/// Where a book came from, and therefore what Libation is allowed to assume about it.
/// <para>
/// The data model was built around Audible: <see cref="Book.AudibleProductId"/> is required and
/// the library scan, the better-quality scan and the "locate audiobooks" matcher all key off it.
/// Rather than fabricate ASINs for books that have none — which would quietly feed those three
/// features rubbish — every book now says what it is.
/// </para>
/// <para>
/// <see cref="Audible"/> is 0 so the migration leaves every existing row exactly as it was.
/// </para>
/// </summary>
public enum BookSource
{
	/// <summary>Downloaded from Audible. The default, and the only source with a real ASIN.</summary>
	Audible = 0,

	/// <summary>
	/// A file already on disk: bought DRM-free from Libro.fm, Downpour or Authors Direct,
	/// ripped from CD, fetched from LibriVox, or inherited from another library manager.
	/// Libation did not download it and will not try to.
	/// </summary>
	Local = 1,

	/// <summary>
	/// A book with no file at all, recorded so the shelf is complete.
	/// <para>
	/// This exists for stores whose audio cannot legitimately leave their app — Chirp being the
	/// case that prompted it. The entry says "you own this", nothing more: there is no path, no
	/// download action, and Libation neither has nor wants the audio. Entered by hand or from a
	/// pasted list, never by reading a retailer's account.
	/// </para>
	/// </summary>
	CatalogueOnly = 2
}
