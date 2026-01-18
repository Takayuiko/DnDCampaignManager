using DnDCampaignManager.Api.Models;
using System.Security.Cryptography;

namespace DnDCampaignManager.Api.Services
{
    public static class RefreshTokenService
    {
        public static RefreshToken Create(int userId)
        {
            return new RefreshToken
            {
                Token = GenerateToken(),
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }
}
