using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace LLMDiscordBot.Commands;

/// <summary>
/// Autocomplete handler for guild ID selection
/// Provides a list of guilds the bot is currently in
/// </summary>
public class GuildAutocompleteHandler(DiscordSocketClient client) : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        // Get the user's current input
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        // Get all guilds the bot is in
        var guilds = client.Guilds
            .OrderBy(g => g.Name)
            .ToList();

        // Filter guilds based on user input (search by name or ID)
        var filteredGuilds = guilds.Where(g =>
            g.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
            g.Id.ToString().Contains(userInput)
        ).ToList();

        // Discord limits autocomplete to 25 options
        var results = filteredGuilds
            .Take(25)
            .Select(g => new AutocompleteResult(
                name: $"{g.Name} ({g.Id})",
                value: g.Id.ToString()
            ))
            .ToList();

        return await Task.FromResult(
            AutocompletionResult.FromSuccess(results)
        );
    }
}

