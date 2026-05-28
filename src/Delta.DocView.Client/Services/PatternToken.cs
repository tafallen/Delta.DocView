namespace Delta.DocView.Client.Services;

/// <summary>
/// A token produced by <see cref="PatternTokeniser.Tokenise(string)"/>.
/// Either static prose or a parameter token with optional name + type.
/// </summary>
public abstract record PatternToken;

public sealed record StaticText(string Text) : PatternToken;

public sealed record ParamToken(string? Name, string Type) : PatternToken;
