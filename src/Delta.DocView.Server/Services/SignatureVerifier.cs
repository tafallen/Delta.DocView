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
            var canonical = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            var actualDigest = Convert.ToHexString(hash).ToLowerInvariant();

            return string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
