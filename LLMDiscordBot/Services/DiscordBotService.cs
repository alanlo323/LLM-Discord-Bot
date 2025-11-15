using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using LLMDiscordBot.Configuration;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Discord Bot service that manages the bot lifecycle
/// </summary>
public class DiscordBotService : IHostedService
{
    private readonly DiscordSocketClient client;
    private readonly ILogger logger;
    private readonly Configuration.DiscordConfig config;
    private readonly CommandHandlerService commandHandler;

    public DiscordBotService(
        DiscordSocketClient client,
        IOptions<Configuration.DiscordConfig> discordConfig,
        ILogger logger,
        CommandHandlerService commandHandler)
    {
        this.client = client;
        this.config = discordConfig.Value;
        this.logger = logger;
        this.commandHandler = commandHandler;

        // Event handlers
        this.client.Log += LogAsync;
        this.client.Ready += ReadyAsync;
    }

    public DiscordSocketClient Client => client;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.Information("Starting Discord Bot Service...");

        if (string.IsNullOrEmpty(config.Token))
        {
            logger.Fatal("Discord bot token is not configured!");
            throw new InvalidOperationException("Discord bot token is not configured!");
        }

        await client.LoginAsync(TokenType.Bot, config.Token);
        
        // Initialize command handler BEFORE starting the client
        await commandHandler.InitializeAsync(client);
        
        await client.StartAsync();

        logger.Information("Discord Bot Service started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.Information("Stopping Discord Bot Service...");
        await client.StopAsync();
        await client.LogoutAsync();
        // Note: Don't dispose client here - it's managed by DI container
        logger.Information("Discord Bot Service stopped");
    }

    private Task LogAsync(LogMessage log)
    {
        var logLevel = log.Severity switch
        {
            LogSeverity.Critical => Serilog.Events.LogEventLevel.Fatal,
            LogSeverity.Error => Serilog.Events.LogEventLevel.Error,
            LogSeverity.Warning => Serilog.Events.LogEventLevel.Warning,
            LogSeverity.Info => Serilog.Events.LogEventLevel.Information,
            LogSeverity.Verbose => Serilog.Events.LogEventLevel.Verbose,
            LogSeverity.Debug => Serilog.Events.LogEventLevel.Debug,
            _ => Serilog.Events.LogEventLevel.Information
        };

        logger.Write(logLevel, log.Exception, "[Discord] {Message}", log.Message);
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        logger.Information("Bot is connected and ready! Logged in as {Username}#{Discriminator}", 
            client.CurrentUser.Username, client.CurrentUser.Discriminator);
        
        await client.SetGameAsync("與 LLM 聊天 | /chat", type: ActivityType.Playing);
        
        // Register slash commands after client is fully ready
        await commandHandler.RegisterCommandsAsync();
    }
}
