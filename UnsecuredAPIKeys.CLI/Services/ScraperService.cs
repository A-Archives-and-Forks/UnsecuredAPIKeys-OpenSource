using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using UnsecuredAPIKeys.Data;
using UnsecuredAPIKeys.Data.Common;
using UnsecuredAPIKeys.Data.Models;
using UnsecuredAPIKeys.Providers;
using UnsecuredAPIKeys.Providers._Interfaces;
using UnsecuredAPIKeys.Providers.Search_Providers;

namespace UnsecuredAPIKeys.CLI.Services;

/// <summary>
/// Scraper service for finding API keys on GitHub.
/// Lite version: GitHub only, 3 AI providers.
/// Full version: www.UnsecuredAPIKeys.com
/// </summary>
public class ScraperService
{
    private readonly DBContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ScraperService>? _logger;
    private readonly IReadOnlyList<IApiKeyProvider> _providers;
    private CancellationTokenSource? _cancellationTokenSource;

    private int _newKeysFound;
    private int _duplicateKeysFound;

    public ScraperService(DBContext dbContext, IHttpClientFactory httpClientFactory, ILogger<ScraperService>? logger = null)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _providers = ApiProviderRegistry.ScraperProviders;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        AnsiConsole.MarkupLine("[cyan]Starting GitHub scraper...[/]");
        AnsiConsole.MarkupLine($"[dim]Loaded {_providers.Count} API key providers[/]");

        foreach (var provider in _providers)
        {
            AnsiConsole.MarkupLine($"  [dim]- {Markup.Escape(provider.ProviderName)}[/]");
        }

        // Get GitHub token
        var token = await _dbContext.SearchProviderTokens
            .Where(t => t.IsEnabled && t.SearchProvider == SearchProviderEnum.GitHub)
            .FirstOrDefaultAsync(cancellationToken);

        if (token == null)
        {
            AnsiConsole.MarkupLine("[red]No GitHub token configured. Use 'Configure Settings' to add one.[/]");
            return;
        }

        // Run continuously
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                await RunScrapingCycleAsync(token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                // Wait before next cycle
                AnsiConsole.MarkupLine($"[dim]Waiting {LiteLimits.SEARCH_DELAY_MS / 1000}s before next search...[/]");
                await Task.Delay(LiteLimits.SEARCH_DELAY_MS, _cancellationTokenSource.Token);

                // Reset counters
                _newKeysFound = 0;
                _duplicateKeysFound = 0;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error during scraping: {Markup.Escape(ex.Message)}[/]");
                _logger?.LogError(ex, "Scraping cycle error");
                await Task.Delay(5000, _cancellationTokenSource.Token);
            }
        }

        AnsiConsole.MarkupLine("[green]Scraper stopped.[/]");
    }

    private async Task RunScrapingCycleAsync(SearchProviderToken token)
    {
        // Get next query to process
        var query = await _dbContext.SearchQueries
            .Where(x => x.IsEnabled && x.LastSearchUTC < DateTime.UtcNow.AddHours(-1))
            .OrderBy(x => x.LastSearchUTC)
            .FirstOrDefaultAsync(_cancellationTokenSource!.Token);

        if (query == null)
        {
            AnsiConsole.MarkupLine("[dim]No queries due for search. Waiting...[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]Searching: {Markup.Escape(query.Query)}[/]");

        // Update last search time
        query.LastSearchUTC = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(_cancellationTokenSource.Token);

        // Search GitHub
        var searchProvider = new GitHubSearchProvider(_dbContext);
        IEnumerable<RepoReference>? results;

        try
        {
            results = await searchProvider.SearchAsync(query, token);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Search error: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        if (results == null)
        {
            AnsiConsole.MarkupLine("[yellow]No results from search.[/]");
            return;
        }

        var resultsList = results.ToList();
        AnsiConsole.MarkupLine($"[dim]Found {resultsList.Count} potential matches[/]");

        // Process each result
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[cyan]Processing results[/]", maxValue: resultsList.Count);

                foreach (var repoRef in resultsList)
                {
                    if (_cancellationTokenSource!.Token.IsCancellationRequested)
                        break;

                    await ProcessResultAsync(repoRef, token, query);
                    task.Increment(1);
                }
            });

        // Summary
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Metric[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("Query", Markup.Escape(query.Query));
        table.AddRow("Results Processed", resultsList.Count.ToString());
        table.AddRow("New Keys", $"[green]{_newKeysFound}[/]");
        table.AddRow("Duplicates", $"[dim]{_duplicateKeysFound}[/]");

        AnsiConsole.Write(table);
    }

    private async Task ProcessResultAsync(RepoReference repoRef, SearchProviderToken token, SearchQuery query)
    {
        try
        {
            // Get file content
            var content = await FetchFileContentAsync(repoRef, token);
            if (string.IsNullOrEmpty(content))
                return;

            // Search for API keys using all provider patterns
            foreach (var provider in _providers)
            {
                foreach (var pattern in provider.RegexPatterns)
                {
                    var regex = new System.Text.RegularExpressions.Regex(pattern);
                    var matches = regex.Matches(content);

                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        var apiKey = match.Value;

                        // Check if already exists
                        var exists = await _dbContext.APIKeys
                            .AnyAsync(k => k.ApiKey == apiKey, _cancellationTokenSource!.Token);

                        if (exists)
                        {
                            Interlocked.Increment(ref _duplicateKeysFound);
                            continue;
                        }

                        // Add new key
                        var newKey = new APIKey
                        {
                            ApiKey = apiKey,
                            ApiType = provider.ApiType,
                            Status = ApiStatusEnum.Unverified,
                            SearchProvider = SearchProviderEnum.GitHub,
                            FirstFoundUTC = DateTime.UtcNow,
                            LastFoundUTC = DateTime.UtcNow
                        };

                        // Add repo reference
                        repoRef.SearchQueryId = query.Id;
                        repoRef.FoundUTC = DateTime.UtcNow;
                        repoRef.Provider = "GitHub";
                        newKey.References.Add(repoRef);

                        _dbContext.APIKeys.Add(newKey);
                        await _dbContext.SaveChangesAsync(_cancellationTokenSource!.Token);

                        Interlocked.Increment(ref _newKeysFound);
                        AnsiConsole.MarkupLine($"[green]+ New {Markup.Escape(provider.ProviderName)} key found![/]");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error processing result: {Url}", repoRef.FileURL);
        }
    }

    private async Task<string?> FetchFileContentAsync(RepoReference repoRef, SearchProviderToken token)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UnsecuredAPIKeys-Lite/1.0");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            // Build raw content URL from repo info
            // Format: https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}
            string? url = null;

            if (!string.IsNullOrEmpty(repoRef.RepoOwner) &&
                !string.IsNullOrEmpty(repoRef.RepoName) &&
                !string.IsNullOrEmpty(repoRef.FilePath))
            {
                var branch = repoRef.Branch ?? "main";
                url = $"https://raw.githubusercontent.com/{repoRef.RepoOwner}/{repoRef.RepoName}/{branch}/{repoRef.FilePath}";
            }

            if (string.IsNullOrEmpty(url))
                return null;

            var response = await client.GetAsync(url, _cancellationTokenSource!.Token);

            // Try 'master' if 'main' fails
            if (!response.IsSuccessStatusCode && repoRef.Branch == null)
            {
                url = $"https://raw.githubusercontent.com/{repoRef.RepoOwner}/{repoRef.RepoName}/master/{repoRef.FilePath}";
                response = await client.GetAsync(url, _cancellationTokenSource!.Token);
            }

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
        }
        catch
        {
            return null;
        }
    }
}
