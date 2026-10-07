using DnDCampaignManager.Api.Models;
using System.Security.Cryptography;
using System.Text;

namespace DnDCampaignManager.Api.Services
{
    public static class RefreshTokenService
    {
        public static RefreshToken Create(int userId, out string token)
        {
            token = GenerateToken();
            return new RefreshToken
            {
                TokenHash = Hash(token),
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };
        }

        // Tokens contain 512 random bits, so a deterministic SHA-256 digest
        // supports indexed lookups without storing the bearer credential.
        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }
}
