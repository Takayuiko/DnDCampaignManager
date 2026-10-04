using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Models.AI;
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
        public DbSet<CharacterAttack> CharacterAttacks => Set<CharacterAttack>();
        public DbSet<CharacterClassOption> CharacterClassOptions => Set<CharacterClassOption>();
        public DbSet<CharacterRaceOption> CharacterRaceOptions => Set<CharacterRaceOption>();
        public DbSet<CharacterBackgroundOption> CharacterBackgroundOptions => Set<CharacterBackgroundOption>();
        public DbSet<AIConversation> AIConversations => Set<AIConversation>();
        public DbSet<AIMessage> AIMessages => Set<AIMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Campaign>()
                .HasOne(c => c.Owner)
                .WithMany(u => u.OwnedCampaigns)
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.NoAction);

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
                .HasIndex(c => new { c.CampaignId, c.UserId })
                .IsUnique();

            modelBuilder.Entity<Character>()
                .HasOne(c => c.User)
                .WithMany(u => u.Characters)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Character>()
                .HasOne(c => c.Campaign)
                .WithMany(ca => ca.Characters)
                .HasForeignKey(c => c.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<CharacterClassOption>()
                .HasIndex(c => new { c.UserId, c.NormalizedName, c.CampaignId })
                .IsUnique();

            modelBuilder.Entity<CharacterClassOption>()
                .HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CharacterClassOption>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CharacterRaceOption>()
                .HasIndex(x => new { x.UserId, x.NormalizedName, x.CampaignId })
                .IsUnique();

            modelBuilder.Entity<CharacterRaceOption>()
                .HasOne(x => x.Campaign)
                .WithMany()  
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CharacterRaceOption>()
                .HasOne(x => x.User)
                .WithMany() 
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<CharacterBackgroundOption>()
                .HasIndex(x => new { x.UserId, x.NormalizedName, x.CampaignId })
                .IsUnique();

            modelBuilder.Entity<CharacterBackgroundOption>()
                .HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CharacterBackgroundOption>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AIConversation>()
                .HasIndex(x => new { x.UserId, x.UpdatedAtUtc });

            modelBuilder.Entity<AIConversation>()
                .HasOne(x => x.User)
                .WithMany(u => u.AIConversations)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AIMessage>()
                .HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });

            modelBuilder.Entity<AIMessage>()
                .HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AIMessage>()
                .Property(x => x.EstimatedCostUsd)
                .HasPrecision(18, 8);
        }
    }
}
