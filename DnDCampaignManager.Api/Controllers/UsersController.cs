using DnDCampaignManager.Api.DTOs;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api")]
public class UsersController : ControllerBase
{
    private readonly DnDxDbContext _dndContext;

    public UsersController(DnDxDbContext db)
    {
        _dndContext = db;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponseDto>> Me()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized();

        var userId = int.Parse(userIdClaim.Value);

        var user = await _dndContext.Users
            .Where(user => user.Id == userId)
            .Select(u => new MeResponseDto
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role,
                IsAdmin = u.IsAdmin
            })
            .SingleOrDefaultAsync();

        if (user == null)
            return Unauthorized();

        return Ok(user);
    }
}
