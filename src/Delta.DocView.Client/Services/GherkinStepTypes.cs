namespace Delta.DocView.Client.Services;

public static class GherkinStepTypes
{
    public const string Given = "Given";
    public const string When  = "When";
    public const string Then  = "Then";
    public const string And   = "And";

    public static readonly IReadOnlyList<string> All =
        new[] { Given, When, Then, And };
}
