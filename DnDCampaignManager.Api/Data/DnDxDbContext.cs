using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DnDCampingManager.Api.Data
{
    public class DnDxDbContext : DbContext
    {
        public DnDxDbContext(DbContextOptions<DnDxDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Campaign> Campaigns => Set<Campaign>();
        public DbSet<Character> Characters => Set<Character>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<CampaignPlayer> CampaignPlayer => Set<CampaignPlayer>();
        public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
        public DbSet<CharacterAttack> CharacterAttacks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Campaign>()
                .HasOne(c => c.Owner)
                .WithMany(u => u.OwnedCampaigns)
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CampaignPlayer>()
                .HasKey(cp => new { cp.CampaignId, cp.UserId });

            modelBuilder.Entity<CampaignPlayer>()
                .HasOne(cp => cp.Campaign)
                .WithMany(c => c.Players)
                .HasForeignKey(cp => cp.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CampaignPlayer>()
                .HasOne(cp => cp.User)
                .WithMany(u => u.Campaigns)
                .HasForeignKey(cp => cp.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Character>()
                .HasOne(c => c.User)
                .WithMany(u => u.Characters)
                .HasForeignKey(c => c.UserId);

            modelBuilder.Entity<Character>()
                .HasOne(c => c.Campaign)
                .WithMany(ca => ca.Characters)
                .HasForeignKey(c => c.CampaignId);

            modelBuilder.Entity<Character>()
                .HasIndex(c => new { c.CampaignId, c.UserId })
                .IsUnique();

            modelBuilder.Entity<CharacterSkill>()
                .HasOne(cs => cs.Character)
                .WithMany(c => c.Skills)
                .HasForeignKey(cs => cs.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CharacterSkill>()
                .HasIndex(cs => new { cs.CharacterId, cs.Skill })
                .IsUnique();

            modelBuilder.Entity<CharacterAttack>()
                .HasOne(a => a.Character)
                .WithMany(c => c.Attacks)
                .HasForeignKey(a => a.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CharacterAttack>()
                .Property(a => a.Name)
                .HasMaxLength(200);

            modelBuilder.Entity<CharacterAttack>()
                .Property(a => a.Damage)
                .HasMaxLength(200);
        }
    }
}
