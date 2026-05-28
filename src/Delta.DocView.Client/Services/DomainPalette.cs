using System.Text;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Deterministic mapping from domain id to a hue in [0, 360).
/// Uses FNV-1a 32-bit hash so the mapping is stable across runs
/// (unlike <see cref="string.GetHashCode()"/>).
/// </summary>
public static class DomainPalette
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    public static int HueFor(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return 0;
        }

        var bytes = Encoding.UTF8.GetBytes(id);
        uint hash = FnvOffsetBasis;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= FnvPrime;
        }

        return (int)(hash % 360u);
    }

    public static string CssVarValue(string id)
        => $"hsl({HueFor(id)} 60% 45%)";
}
