namespace Delta.DocView.Client.Services;

/// <summary>One keyboard shortcut for display in the shortcuts overlay.
/// <paramref name="Keys"/> are alternative bindings for the same action,
/// each rendered as a &lt;kbd&gt; chip joined by "or".</summary>
public sealed record Shortcut(string Label, IReadOnlyList<string> Keys);
