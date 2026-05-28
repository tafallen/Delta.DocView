namespace Delta.DocView.Client.Services;

/// <summary>
/// Centralised UI copy for stubs and placeholders that future stories will replace.
/// </summary>
public static class UiStrings
{
    public static string DisplayParamType(string type) =>
        type.Equals("DocString", StringComparison.OrdinalIgnoreCase) ? "string" : type;
}
