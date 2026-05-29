namespace Delta.DocView.Client.Services;

/// <summary>Current user info fetched from /api/user at boot.</summary>
public sealed record UserInfo(string Name, string Initials, bool Authenticated)
{
    public static readonly UserInfo Fallback = new("QA", "QA", false);
}
