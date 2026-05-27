using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Delta.DocView.Server.Services;

public static class SignatureVerifier
{
    public static bool Verify(string rawJson)
    {
        try
        {
            var root = JsonNode.Parse(rawJson)?.AsObject();
            if (root is null) return false;

            var signatureNode = root["signature"];
            if (signatureNode is null) return false;

            var expectedDigest = signatureNode["digest"]?.GetValue<string>();
            if (string.IsNullOrEmpty(expectedDigest)) return false;

            root.Remove("signature");
            var canonical = Canonicalise(root);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            var actualDigest = Convert.ToHexString(hash).ToLowerInvariant();

            return string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // Serialises a JsonObject with all keys sorted lexicographically (recursive),
    // producing a stable canonical form regardless of the original property order.
    private static string Canonicalise(JsonNode node)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
        WriteCanonical(writer, node);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var kv in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(kv.Key);
                    WriteCanonical(writer, kv.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonArray arr:
                writer.WriteStartArray();
                foreach (var item in arr)
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;

            default:
                // Scalar — let the node write itself
                (node ?? JsonValue.Create<object?>(null))
                    .WriteTo(writer, new JsonSerializerOptions());
                break;
        }
    }
}
