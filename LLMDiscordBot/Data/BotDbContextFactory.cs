using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LLMDiscordBot.Data;

/// <summary>
/// Design-time factory for creating DbContext instances for migrations
/// </summary>
public class BotDbContextFactory : IDesignTimeDbContextFactory<BotDbContext>
{
    public BotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BotDbContext>();
        optionsBuilder.UseSqlite("Data Source=llmbot.db");

        return new BotDbContext(optionsBuilder.Options);
    }
}

