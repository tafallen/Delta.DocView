using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Delta.DocView.Server.Services;

namespace Delta.DocView.Tests.Services;

public class SignatureVerifierTests
{
    private static string BuildRawJson(string digest)
    {
        var json = """
            {
              "$schema": "https://delta.docgen/schema/v1",
              "version": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z",
              "generatorVersion": "1.0.0",
              "enriched": true,
              "domains": [],
              "steps": [],
              "signature": { "algorithm": "SHA-256", "digest": "PLACEHOLDER" }
            }
            """;
        return json.Replace("PLACEHOLDER", digest);
    }

    private static string ComputeExpectedDigest(string rawJson)
    {
        var node = JsonNode.Parse(rawJson)!.AsObject();
        node.Remove("signature");
        // Must use the same canonical form as SignatureVerifier: sorted keys, recursive
        var canonical = Canonicalise(node);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Canonicalise(JsonNode node)
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = false });
        WriteCanonical(writer, node);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCanonical(System.Text.Json.Utf8JsonWriter writer, JsonNode? node)
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
                (node ?? JsonValue.Create<object?>(null))
                    .WriteTo(writer, new JsonSerializerOptions());
                break;
        }
    }

    [Fact]
    public void Verify_CorrectDigest_ReturnsTrue()
    {
        var rawJson = BuildRawJson("placeholder");
        var correctDigest = ComputeExpectedDigest(rawJson);
        rawJson = BuildRawJson(correctDigest);

        var result = SignatureVerifier.Verify(rawJson);

        Assert.True(result);
    }

    [Fact]
    public void Verify_WrongDigest_ReturnsFalse()
    {
        var rawJson = BuildRawJson("0000000000000000000000000000000000000000000000000000000000000000");

        var result = SignatureVerifier.Verify(rawJson);

        Assert.False(result);
    }

    [Fact]
    public void Verify_NoSignatureProperty_ReturnsFalse()
    {
        var json = """{ "version": "1.0.0" }""";

        var result = SignatureVerifier.Verify(json);

        Assert.False(result);
    }

    [Fact]
    public void Verify_PropertiesInDifferentOrder_StillReturnsTrue()
    {
        // Build canonical JSON, compute its digest, then present the same data
        // with properties in a completely different order — verifier must still pass.
        var canonical = BuildRawJson("placeholder");
        var correctDigest = ComputeExpectedDigest(canonical);

        // Reorder: put "signature" first and shuffle the other fields
        var reordered = $$"""
            {
              "signature": { "algorithm": "SHA-256", "digest": "{{correctDigest}}" },
              "steps": [],
              "enriched": true,
              "domains": [],
              "generatorVersion": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z",
              "version": "1.0.0",
              "$schema": "https://delta.docgen/schema/v1"
            }
            """;

        Assert.True(SignatureVerifier.Verify(reordered));
    }
}
