using Microsoft.EntityFrameworkCore;
using LLMDiscordBot.Models;

namespace LLMDiscordBot.Data;

/// <summary>
/// Database context for the LLM Discord Bot
/// </summary>
public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<TokenUsage> TokenUsages { get; set; } = null!;
    public DbSet<ChatHistory> ChatHistories { get; set; } = null!;
    public DbSet<BotSettings> BotSettings { get; set; } = null!;
    public DbSet<GuildSettings> GuildSettings { get; set; } = null!;
    public DbSet<GuildAdmin> GuildAdmins { get; set; } = null!;
    public DbSet<UserPreferences> UserPreferences { get; set; } = null!;
    public DbSet<InteractionLog> InteractionLogs { get; set; } = null!;
    public DbSet<TaskSession> TaskSessions { get; set; } = null!;
    public DbSet<TaskPlanStep> TaskPlanSteps { get; set; } = null!;
    public DbSet<ActionApprovalLog> ActionApprovalLogs { get; set; } = null!;
    public DbSet<MonitoredTask> MonitoredTasks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.IsBlocked);
            entity.HasIndex(e => e.CreatedAt);
        });

        // TokenUsage entity configuration
        modelBuilder.Entity<TokenUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Fixed: Include GuildId in unique index to support per-guild tracking
            entity.HasIndex(e => new { e.UserId, e.GuildId, e.Date }).IsUnique();
            // Add separate index on GuildId for guild-specific queries
            entity.HasIndex(e => e.GuildId);
            entity.HasIndex(e => e.Date);
            entity.HasOne(e => e.User)
                .WithMany(u => u.TokenUsages)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatHistory entity configuration
        modelBuilder.Entity<ChatHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ChannelId, e.Timestamp });
            // Add index on GuildId for guild-specific queries
            entity.HasIndex(e => e.GuildId);
            entity.HasOne(e => e.User)
                .WithMany(u => u.ChatHistories)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BotSettings entity configuration
        modelBuilder.Entity<BotSettings>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => e.UpdatedAt);
        });

        // GuildSettings entity configuration
        modelBuilder.Entity<GuildSettings>(entity =>
        {
            entity.HasKey(e => e.GuildId);
            entity.Property(e => e.GuildId).ValueGeneratedNever(); // Discord Guild ID, not auto-generated
            entity.HasIndex(e => e.UpdatedAt);
        });

        // GuildAdmin entity configuration
        modelBuilder.Entity<GuildAdmin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GuildId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.GuildId);
            entity.HasIndex(e => e.UserId);
        });

        // UserPreferences entity configuration
        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedNever(); // Discord User ID, not auto-generated
            entity.HasIndex(e => e.LastInteractionAt);
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<UserPreferences>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InteractionLog entity configuration
        modelBuilder.Entity<InteractionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            entity.HasIndex(e => e.GuildId);
            entity.HasIndex(e => e.CommandType);
            entity.HasIndex(e => e.Timestamp);
        });

        // TaskSession entity configuration
        modelBuilder.Entity<TaskSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.HasIndex(e => new { e.UserId, e.GuildId, e.Status, e.IsArchived });
            entity.HasIndex(e => e.UpdatedAt);
            entity.Property(e => e.ApprovalPolicy).HasMaxLength(50);
            entity.Property(e => e.AllowedWebsites).HasMaxLength(1000);
            entity.Property(e => e.MemoryControllerKey).HasMaxLength(200);
            entity.HasMany(e => e.Steps)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.TaskSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ApprovalLogs)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.TaskSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Monitor)
                .WithOne(e => e.Session)
                .HasForeignKey<MonitoredTask>(e => e.TaskSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TaskPlanStep entity configuration
        modelBuilder.Entity<TaskPlanStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.HasIndex(e => new { e.TaskSessionId, e.SequenceNumber });
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.ToolName).HasMaxLength(100);
            entity.HasMany(e => e.ApprovalLogs)
                .WithOne(e => e.Step)
                .HasForeignKey(e => e.TaskPlanStepId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ActionApprovalLog entity configuration
        modelBuilder.Entity<ActionApprovalLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.HasIndex(e => new { e.TaskSessionId, e.Status });
            entity.HasIndex(e => new { e.RequestedBy, e.Status });
            entity.HasIndex(e => new { e.ApproverUserId, e.Status });
        });

        // MonitoredTask entity configuration
        modelBuilder.Entity<MonitoredTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.HasIndex(e => new { e.Status, e.NextCheckAt });
        });

        // Seed default settings
        modelBuilder.Entity<BotSettings>().HasData(
            new BotSettings { Key = "Model", Value = "default", UpdatedAt = DateTime.UtcNow },
            new BotSettings { Key = "Temperature", Value = "0.7", UpdatedAt = DateTime.UtcNow },
            new BotSettings { Key = "GlobalMaxTokens", Value = "2000", UpdatedAt = DateTime.UtcNow },
            //new BotSettings { Key = "GlobalSystemPrompt", Value = "You are a helpful AI assistant.", UpdatedAt = DateTime.UtcNow },
            new BotSettings { Key = "GlobalDailyLimit", Value = "100000", UpdatedAt = DateTime.UtcNow }
        );
    }
}

