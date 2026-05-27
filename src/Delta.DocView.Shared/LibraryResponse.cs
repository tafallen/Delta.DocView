using Delta.DocView.Shared.Models;

namespace Delta.DocView.Shared;

/// <summary>
/// Returned by GET /api/library. Null Warning means the signature verified cleanly.
/// </summary>
public sealed record LibraryResponse(StepLibrary Library, string? Warning);
