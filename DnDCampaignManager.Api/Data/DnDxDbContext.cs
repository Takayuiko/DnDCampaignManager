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
        public DbSet<CampaignItem> CampaignItems => Set<CampaignItem>();
        public DbSet<CharacterItem> CharacterItems => Set<CharacterItem>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<CampaignPlayer> CampaignPlayer => Set<CampaignPlayer>();
        public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
        public DbSet<CharacterAttack> CharacterAttacks => Set<CharacterAttack>();
        public DbSet<CharacterClassOption> CharacterClassOptions => Set<CharacterClassOption>();
        public DbSet<CharacterRaceOption> CharacterRaceOptions => Set<CharacterRaceOption>();
        public DbSet<CharacterBackgroundOption> CharacterBackgroundOptions => Set<CharacterBackgroundOption>();
        public DbSet<AIConversation> AIConversations => Set<AIConversation>();
        public DbSet<AIMessage> AIMessages => Set<AIMessage>();
        public DbSet<CampaignSessionNote> CampaignSessionNotes => Set<CampaignSessionNote>();
        public DbSet<CampaignKnowledgeChunk> CampaignKnowledgeChunks => Set<CampaignKnowledgeChunk>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CampaignItem>().HasAlternateKey(x => new { x.Id, x.CampaignId });
            modelBuilder.Entity<CampaignItem>().HasIndex(x => new { x.CampaignId, x.NormalizedName }).IsUnique();
            modelBuilder.Entity<CampaignItem>().HasOne(x => x.Campaign).WithMany()
                .HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CampaignItem>().Property(x => x.Name).HasMaxLength(120);
            modelBuilder.Entity<CampaignItem>().Property(x => x.NormalizedName).HasMaxLength(120);
            modelBuilder.Entity<CampaignItem>().Property(x => x.Category).HasMaxLength(50);
            modelBuilder.Entity<CampaignItem>().Property(x => x.Description).HasMaxLength(4000);
            modelBuilder.Entity<CampaignItem>().Property(x => x.Source).HasMaxLength(50);
            modelBuilder.Entity<CampaignItem>().Property(x => x.WeightLb).HasPrecision(18, 4);
            modelBuilder.Entity<CampaignItem>().Property(x => x.CostGp).HasPrecision(18, 4);
            modelBuilder.Entity<CampaignItem>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_CampaignItems_Weight", "\"WeightLb\" IS NULL OR \"WeightLb\" >= 0");
                t.HasCheckConstraint("CK_CampaignItems_Cost", "\"CostGp\" IS NULL OR \"CostGp\" >= 0");
            });
            modelBuilder.Entity<Character>().HasAlternateKey(x => new { x.Id, x.CampaignId });
            modelBuilder.Entity<CharacterItem>().HasOne(x => x.Character).WithMany()
                .HasForeignKey(x => new { x.CharacterId, x.CampaignId })
                .HasPrincipalKey(x => new { x.Id, x.CampaignId }).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CharacterItem>().HasOne(x => x.Item).WithMany()
                .HasForeignKey(x => new { x.ItemId, x.CampaignId })
                .HasPrincipalKey(x => new { x.Id, x.CampaignId }).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<CharacterItem>().Property(x => x.Notes).HasMaxLength(2000);
            modelBuilder.Entity<CharacterItem>().ToTable(t => t.HasCheckConstraint("CK_CharacterItems_Quantity", "\"Quantity\" > 0"));
            modelBuilder.Entity<User>().ToTable(t => t.HasCheckConstraint("CK_Users_AdminMustBeDM", "NOT \"IsAdmin\" OR \"Role\" = 'DM'"));
            modelBuilder.Entity<AIConversation>().HasIndex(x => new { x.UserId, x.CampaignId }).IsUnique();
            modelBuilder.Entity<AIConversation>().HasOne(x => x.Campaign).WithMany()
                .HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CampaignSessionNote>().HasOne(x => x.Campaign).WithMany()
                .HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CampaignSessionNote>().HasIndex(x => new { x.CampaignId, x.SessionNumber });
            modelBuilder.Entity<CampaignSessionNote>().Property(x => x.Title).HasMaxLength(160);
            modelBuilder.Entity<CampaignKnowledgeChunk>().HasOne(x => x.SessionNote).WithMany(x => x.Chunks)
                .HasForeignKey(x => x.SessionNoteId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CampaignKnowledgeChunk>().HasIndex(x => new { x.SessionNoteId, x.Position }).IsUnique();
            modelBuilder.Entity<CampaignKnowledgeChunk>().Property(x => x.Embedding).HasColumnType("real[]");
            modelBuilder.Entity<AIMessage>().Property(x => x.SourcesJson).HasDefaultValue("[]");
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
