using System.Security.Cryptography;
using System.Text;

namespace Personal.Attendance.Infrastructure.Invitations;

internal static class InvitationToken
{
    public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool Matches(string storedHash, string token)
    {
        var storedHashBytes = Encoding.ASCII.GetBytes(storedHash);
        var submittedHashBytes = Encoding.ASCII.GetBytes(Hash(token));
        return storedHashBytes.Length == submittedHashBytes.Length &&
               CryptographicOperations.FixedTimeEquals(storedHashBytes, submittedHashBytes);
    }

    public static string BuildActivationUrl(string token, string publicBaseUrl)
    {
        var configuredBaseUrl = publicBaseUrl.TrimEnd('/') + "/";
        var activationPage = new Uri(new Uri(configuredBaseUrl, UriKind.Absolute), "activate");
        return $"{activationPage}#token={Uri.EscapeDataString(token)}";
    }
}
