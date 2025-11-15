using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Discord;
using Discord.WebSocket;
using Serilog;
using LLMDiscordBot.Configuration;
using LLMDiscordBot.Data;
using LLMDiscordBot.Services;
using LLMDiscordBot.Plugins;

namespace LLMDiscordBot;

class Program
{
    static async Task Main(string[] args)
    {
        // Configure Serilog
        var configuration = BuildConfiguration();
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting LLM Discord Bot...");

            var host = CreateHostBuilder(args).Build();

            // Ensure database is created and migrations are applied
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<BotDbContext>();
                await context.Database.MigrateAsync();
                Log.Information("Database initialized and migrations applied");
            }

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // Register configuration
                services.Configure<Configuration.DiscordConfig>(configuration.GetSection("Discord"));
                services.Configure<LLMConfig>(configuration.GetSection("LLM"));
                services.Configure<TokenLimitsConfig>(configuration.GetSection("TokenLimits"));
                services.Configure<DatabaseConfig>(configuration.GetSection("Database"));
                services.Configure<McpConfig>(configuration.GetSection("MCP"));

                // Register Serilog logger
                services.AddSingleton(Log.Logger);

                // Register HttpClient for MCP services
                services.AddHttpClient("TavilyMcp", client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
                });

                // Register Discord client with configuration
                services.AddSingleton<DiscordSocketClient>(provider =>
                {
                    var socketConfig = new DiscordSocketConfig
                    {
                        GatewayIntents = GatewayIntents.Guilds |
                                        GatewayIntents.GuildMessages |
                                        GatewayIntents.DirectMessages |
                                        GatewayIntents.MessageContent,
                        AlwaysDownloadUsers = false,
                        MessageCacheSize = 100
                    };
                    return new DiscordSocketClient(socketConfig);
                });

                // Register database context
                var connectionString = configuration.GetSection("Database:ConnectionString").Value 
                    ?? "Data Source=llmbot.db";
                services.AddDbContext<BotDbContext>(options =>
                    options.UseSqlite(connectionString));

                // Register repositories
                services.AddScoped<IRepository, Repository>();

                // Register MCP services
                services.AddSingleton<McpService>();
                services.AddSingleton<TavilySearchPlugin>();

                // Register services (using Scoped to match Repository lifecycle)
                services.AddSingleton<CommandHandlerService>();
                services.AddScoped<LLMService>();  // Changed to Scoped - depends on IRepository
                services.AddScoped<TokenControlService>();  // Changed to Scoped - depends on IRepository
                services.AddScoped<ChatProcessorService>();  // Chat processing service
                services.AddScoped<HabitLearningService>();  // Habit learning service
                services.AddSingleton<UserRequestQueueService>();

                // Register hosted services
                services.AddHostedService<DiscordBotService>();
                services.AddHostedService<DailyCleanupService>();
            });

    static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }
}
