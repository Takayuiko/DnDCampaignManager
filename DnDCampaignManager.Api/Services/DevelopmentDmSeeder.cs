using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DnDCampaignManager.Api.Services;

public static class DevelopmentDmSeeder
{
    public const string AdministratorEmail = "hagges02@gmail.com";
    public static async Task RunAsync(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("DM seeding is only available in Development.");

        var email = configuration["DevelopmentDm:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("DevelopmentDm:Email must be configured.");

        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("DefaultConnection must be configured.");

        var options = new DbContextOptionsBuilder<DnDxDbContext>().UseNpgsql(connection).Options;
        await using var db = new DnDxDbContext(options);
        await db.Database.MigrateAsync();

        var normalizedEmail = email.ToLowerInvariant();
        var matches = await db.Users.Where(u => u.Email.ToLower() == normalizedEmail).Take(2).ToListAsync();
        if (matches.Count > 1)
            throw new InvalidOperationException("Multiple accounts match the email; no account changes were made.");

        var user = matches.SingleOrDefault();
        if (user is null)
        {
            var password = configuration["DevelopmentDm:Password"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("A password is required to create the development account.");

            user = new User { Email = email, Role = Roles.DM };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
            db.Users.Add(user);
        }
        var wasChanged = user.Role != Roles.DM ||
            (email.Equals(AdministratorEmail, StringComparison.OrdinalIgnoreCase) && !user.IsAdmin);
        user.Role = Roles.DM;
        if (email.Equals(AdministratorEmail, StringComparison.OrdinalIgnoreCase)) user.IsAdmin = true;
        if (wasChanged)
        {
            user.TokenVersion++;
            await db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync();
        }

        await db.SaveChangesAsync();
        Console.WriteLine(user.IsAdmin ? "Development DM and administrator account is ready. Existing passwords are unchanged."
            : "Development DM account is ready. Existing passwords are unchanged.");
    }
}
