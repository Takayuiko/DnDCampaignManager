using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DnDxDbContext _dnDxDbContext;

    public DashboardController(DnDxDbContext db)
    {
        _dnDxDbContext = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashbobard()
    {
        var userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        var campaigns = await _dnDxDbContext.Campaigns
            .Where(c =>
                c.OwnerId == userId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                IsOwner = c.OwnerId == userId
            })
            .ToListAsync();

        return Ok(campaigns);
    }
}
