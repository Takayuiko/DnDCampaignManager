using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DnDCampingManager.Api.Data;

// EF commands need only database configuration, not a running API or an OpenAI key.
public sealed class DnDxDbContextFactory : IDesignTimeDbContextFactory<DnDxDbContext>
{
    public DnDxDbContext CreateDbContext(string[] args)
    {
        var directory = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(directory, "appsettings.json")))
            directory = Path.Combine(directory, "DnDCampaignManager.Api");
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder().SetBasePath(directory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<DnDxDbContextFactory>(optional: true)
            .AddEnvironmentVariables().Build();
        return new DnDxDbContext(new DbContextOptionsBuilder<DnDxDbContext>()
            .UseNpgsql(configuration.GetConnectionString("DefaultConnection")).Options);
    }
}
