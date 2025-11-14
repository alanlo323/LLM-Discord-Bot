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
            entity.HasIndex(e => new { e.UserId, e.Date });
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

        // Seed default settings
        modelBuilder.Entity<BotSettings>().HasData(
            new BotSettings { Key = "Model", Value = "default", UpdatedAt = DateTime.UtcNow },
            new BotSettings { Key = "Temperature", Value = "0.7", UpdatedAt = DateTime.UtcNow },
            new BotSettings { Key = "MaxTokens", Value = "2000", UpdatedAt = DateTime.UtcNow },
            new BotSettings { Key = "SystemPrompt", Value = "You are a helpful AI assistant.", UpdatedAt = DateTime.UtcNow },
            new BotSettings { Key = "GlobalDailyLimit", Value = "100000", UpdatedAt = DateTime.UtcNow }
        );
    }
}

