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
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

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
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto Register)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var normalizedEmail = Register.Email.Trim().ToLowerInvariant();
            if (await _dndContext.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail))
                return BadRequest(new { message = "Email already registered" });

            var user = new User
            {
                Email = normalizedEmail,
                Role = Roles.Player
            };

            user.PasswordHash = _hasher.HashPassword(user, Register.Password);

            var refreshToken = RefreshTokenService.Create(user.Id, out var rawToken);
            user.RefreshTokens.Add(refreshToken);
            _dndContext.Users.Add(user);
            try
            {
                await _dndContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Users_NormalizedEmail" or "IX_Users_Email" })
            {
                return BadRequest(new { message = "Email already registered" });
            }

            var accessToken = _jwtService.GenerateToken(user);

            Response.Cookies.Append("refreshToken", rawToken, new CookieOptions
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
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto login)
        {
            var normalizedEmail = login.Email.Trim().ToLowerInvariant();
            var user = await _dndContext.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

            if (user == null)
                return Unauthorized();

            var result = _hasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                login.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized();

            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = RefreshTokenService.Create(user.Id, out var rawToken);

            _dndContext.RefreshTokens.Add(refreshToken);
            await _dndContext.SaveChangesAsync();

            Response.Cookies.Append("refreshToken", rawToken, new CookieOptions
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
        public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim.Value);

            await using var transaction = _dndContext.Database.CurrentTransaction is null
                ? await _dndContext.Database.BeginTransactionAsync(cancellationToken) : null;
            // Refresh and role changes take this same lock before changing tokens.
            var user = await _dndContext.Users
                .FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (user == null)
                return Unauthorized();

            user.TokenVersion++;

            await _dndContext.RefreshTokens.Where(t => t.UserId == userId)
                .ExecuteUpdateAsync(updates => updates.SetProperty(t => t.IsRevoked, true), cancellationToken);
            await _dndContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/auth" });

            return Ok();
        }


        [AllowAnonymous]
        [HttpPost("refresh")]
        [EnableRateLimiting("auth-refresh")]
        public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var token))
                return Unauthorized();

            var tokenHash = RefreshTokenService.Hash(token);
            var userId = await _dndContext.RefreshTokens.AsNoTracking()
                .Where(r => r.TokenHash == tokenHash)
                .Select(r => (int?)r.UserId).SingleOrDefaultAsync(cancellationToken);
            if (userId is null) return Unauthorized();

            await using var transaction = _dndContext.Database.CurrentTransaction is null
                ? await _dndContext.Database.BeginTransactionAsync(cancellationToken) : null;
            // Lock the account first, then recheck validity after any competing
            // refresh, logout or role change has committed.
            var user = await _dndContext.Users
                .FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId.Value} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (user is null) return Unauthorized();

            var stored = await _dndContext.RefreshTokens
                .SingleOrDefaultAsync(r =>
                    r.TokenHash == tokenHash &&
                    r.UserId == user.Id &&
                    !r.IsRevoked &&
                    r.ExpiresAt > DateTime.UtcNow, cancellationToken);

            if (stored == null)
                return Unauthorized();

            stored.IsRevoked = true;

            var newRefreshToken = RefreshTokenService.Create(stored.UserId, out var rawToken);
            stored.ReplacedByTokenHash = newRefreshToken.TokenHash;

            _dndContext.RefreshTokens.Add(newRefreshToken);
            var newAccessToken = _jwtService.GenerateToken(user);
            await _dndContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            // Set new refresh cookie
            Response.Cookies.Append(
                "refreshToken",
                rawToken,
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
