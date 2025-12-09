using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using UnsecuredAPIKeys.Data;
using UnsecuredAPIKeys.Data.Common;
using UnsecuredAPIKeys.Data.Models;

namespace UnsecuredAPIKeys.CLI.Services;

/// <summary>
/// Service for database initialization and common operations.
/// </summary>
public class DatabaseService(string dbPath = "unsecuredapikeys.db")
{
    public async Task<DBContext> InitializeDatabaseAsync()
    {
        var dbContext = new DBContext(dbPath);

        // Ensure database is created
        await dbContext.Database.EnsureCreatedAsync();

        // Seed default data if needed
        await SeedDefaultDataAsync(dbContext);

        return dbContext;
    }

    private async Task SeedDefaultDataAsync(DBContext dbContext)
    {
        // Add default search queries if none exist
        if (!await dbContext.SearchQueries.AnyAsync())
        {
            var defaultQueries = new[]
            {
                // OpenAI patterns
                "sk-proj-",
                "sk-or-v1-",
                "sk-",
                "OPENAI_API_KEY",
                "openai.api_key",
                "chatgpt api key",
                "gpt-4 api key",

                // Anthropic patterns
                "sk-ant-api",
                "ANTHROPIC_API_KEY",
                "anthropic_api_key",
                "claude api key",

                // Google AI patterns
                "AIzaSy",
                "GOOGLE_API_KEY",
                "gemini_api_key",

                // Other AI providers (patterns only, validation limited to lite providers)
                "r8_",           // Replicate
                "fw_",           // Fireworks
                "hf_",           // HuggingFace
                "AI_API_KEY"     // Generic
            };

            foreach (var query in defaultQueries)
            {
                dbContext.SearchQueries.Add(new SearchQuery
                {
                    Query = query,
                    IsEnabled = true,
                    LastSearchUTC = DateTime.UtcNow.AddDays(-1)
                });
            }

            await dbContext.SaveChangesAsync();
            AnsiConsole.MarkupLine($"[dim]Added {defaultQueries.Length} default search queries.[/]");
        }
    }

    public async Task<Statistics> GetStatisticsAsync(DBContext dbContext)
    {
        var stats = new Statistics
        {
            TotalKeys = await dbContext.APIKeys.CountAsync(),
            ValidKeys = await dbContext.APIKeys.CountAsync(k => k.Status == ApiStatusEnum.Valid),
            InvalidKeys = await dbContext.APIKeys.CountAsync(k => k.Status == ApiStatusEnum.Invalid),
            UnverifiedKeys = await dbContext.APIKeys.CountAsync(k => k.Status == ApiStatusEnum.Unverified),
            ValidNoCreditsKeys = await dbContext.APIKeys.CountAsync(k => k.Status == ApiStatusEnum.ValidNoCredits),
            OpenAIKeys = await dbContext.APIKeys.CountAsync(k => k.ApiType == ApiTypeEnum.OpenAI),
            AnthropicKeys = await dbContext.APIKeys.CountAsync(k => k.ApiType == ApiTypeEnum.AnthropicClaude),
            GoogleKeys = await dbContext.APIKeys.CountAsync(k => k.ApiType == ApiTypeEnum.GoogleAI),
            HasGitHubToken = await dbContext.SearchProviderTokens
                .AnyAsync(t => t.IsEnabled && t.SearchProvider == SearchProviderEnum.GitHub)
        };

        return stats;
    }

    public async Task SaveGitHubTokenAsync(DBContext dbContext, string token)
    {
        var existing = await dbContext.SearchProviderTokens
            .FirstOrDefaultAsync(t => t.SearchProvider == SearchProviderEnum.GitHub);

        if (existing != null)
        {
            existing.Token = token;
            existing.IsEnabled = true;
        }
        else
        {
            dbContext.SearchProviderTokens.Add(new SearchProviderToken
            {
                Token = token,
                SearchProvider = SearchProviderEnum.GitHub,
                IsEnabled = true
            });
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        // Reinitialize
        await InitializeDatabaseAsync();
    }

    public async Task ExportKeysAsync(DBContext dbContext, string filePath, bool validOnly, string format)
    {
        var query = dbContext.APIKeys.AsQueryable();

        if (validOnly)
        {
            query = query.Where(k => k.Status == ApiStatusEnum.Valid || k.Status == ApiStatusEnum.ValidNoCredits);
        }

        var keys = await query
            .Include(k => k.References)
            .ToListAsync();

        if (format.ToLower() == "json")
        {
            await ExportAsJsonAsync(keys, filePath);
        }
        else
        {
            await ExportAsCsvAsync(keys, filePath);
        }
    }

    private async Task ExportAsJsonAsync(List<APIKey> keys, string filePath)
    {
        var exportData = keys.Select(k => new
        {
            k.Id,
            k.ApiKey,
            Type = k.ApiType.ToString(),
            Status = k.Status.ToString(),
            k.FirstFoundUTC,
            k.LastCheckedUTC,
            Sources = k.References.Select(r => new
            {
                r.RepoURL,
                r.RepoOwner,
                r.RepoName,
                r.FilePath,
                r.FoundUTC
            })
        });

        var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(filePath, json);
    }

    private async Task ExportAsCsvAsync(List<APIKey> keys, string filePath)
    {
        var lines = new List<string>
        {
            "Id,ApiKey,Type,Status,FirstFoundUTC,LastCheckedUTC,RepoURL"
        };

        foreach (var key in keys)
        {
            var repoUrl = key.References.FirstOrDefault()?.RepoURL ?? "";
            lines.Add($"{key.Id},\"{key.ApiKey}\",{key.ApiType},{key.Status},{key.FirstFoundUTC:O},{key.LastCheckedUTC:O},\"{repoUrl}\"");
        }

        await File.WriteAllLinesAsync(filePath, lines);
    }
}

public class Statistics
{
    public int TotalKeys { get; set; }
    public int ValidKeys { get; set; }
    public int InvalidKeys { get; set; }
    public int UnverifiedKeys { get; set; }
    public int ValidNoCreditsKeys { get; set; }
    public int OpenAIKeys { get; set; }
    public int AnthropicKeys { get; set; }
    public int GoogleKeys { get; set; }
    public bool HasGitHubToken { get; set; }
}
