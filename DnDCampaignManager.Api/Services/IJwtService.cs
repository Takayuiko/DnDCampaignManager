using DnDCampingManager.Api.Models;

namespace DnDCampingManager.Api.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
