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
        var canonical = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
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
}
