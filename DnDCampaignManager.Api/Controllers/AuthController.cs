using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.DTOs;
using DnDCampingManager.Api.Models;
using DnDCampingManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DnDCampingManager.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly DnDxDbContext _dndContext;
        private readonly IPasswordHasher<User> _hasher;
        private readonly IJwtService _jwtService;

        public AuthController(DnDxDbContext db, IPasswordHasher<User> hasher, IJwtService jwt)
        {
            _dndContext = db;
            _hasher = hasher;
            _jwtService = jwt;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto Register)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _dndContext.Users.AnyAsync(u => u.Email == Register.Email))
                return BadRequest(new { message = "Email already registered" });

            var user = new User
            {
                Email = Register.Email,
                Role = Roles.Player
            };

            user.PasswordHash = _hasher.HashPassword(user, Register.Password);

            _dndContext.Users.Add(user);
            await _dndContext.SaveChangesAsync();

            var accessToken = _jwtService.GenerateToken(user);

            var refreshToken = RefreshTokenService.Create(user.Id);
            _dndContext.RefreshTokens.Add(refreshToken);
            await _dndContext.SaveChangesAsync();

            Response.Cookies.Append("refreshToken", refreshToken.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = refreshToken.ExpiresAt,
                Path = "/api/auth"
            });

            return Ok(new { accessToken = accessToken });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto login)
        {
            var user = await _dndContext.Users.SingleOrDefaultAsync(u => u.Email == login.Email);

            if (user == null)
                return Unauthorized();

            var result = _hasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                login.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized();

            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = RefreshTokenService.Create(user.Id);

            _dndContext.RefreshTokens.Add(refreshToken);
            await _dndContext.SaveChangesAsync();

            Response.Cookies.Append("refreshToken", refreshToken.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = refreshToken.ExpiresAt,
                Path = "/api/auth"
            });

            return Ok(new { accessToken = accessToken });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim.Value);

            var user = await _dndContext.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return Unauthorized();

            user.TokenVersion++;

            foreach (var token in user.RefreshTokens)
            {
                token.IsRevoked = true;
            }

            user.TokenVersion++;
            await _dndContext.SaveChangesAsync();

            Response.Cookies.Delete("refreshToken");

            return Ok();
        }


        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var token))
                return Unauthorized();

            var stored = await _dndContext.RefreshTokens    
                .Include(r => r.User)
                .SingleOrDefaultAsync(r =>
                    r.Token == token &&
                    !r.IsRevoked &&
                    r.ExpiresAt > DateTime.UtcNow);

            if (stored == null)
                return Unauthorized();

            stored.IsRevoked = true;

            var newRefreshToken = RefreshTokenService.Create(stored.UserId);
            stored.ReplacedByToken = newRefreshToken.Token;

            _dndContext.RefreshTokens.Add(newRefreshToken);
            await _dndContext.SaveChangesAsync();

            // Issue new access token
            var newAccessToken = _jwtService.GenerateToken(stored.User);

            // Set new refresh cookie
            Response.Cookies.Append(
                "refreshToken",
                newRefreshToken.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = newRefreshToken.ExpiresAt,
                    Path = "/api/auth"
                });

            return Ok(new { accessToken = newAccessToken });
        }

    }

}
