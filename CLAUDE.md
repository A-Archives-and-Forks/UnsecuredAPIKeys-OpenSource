# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**UnsecuredAPIKeys Lite** is a command-line tool for discovering and validating exposed API keys on GitHub. This is the open-source lite version with intentional limitations.

> **Full Version:** [www.UnsecuredAPIKeys.com](https://www.UnsecuredAPIKeys.com)
> Web UI, all providers, community features, and more.

### Lite Version Limits

| Feature | Lite (This Repo) | Full Version |
|---------|------------------|--------------|
| Search Provider | GitHub only | GitHub, GitLab, SourceGraph |
| API Providers | OpenAI, Anthropic, Google | 15+ providers |
| Valid Key Cap | 50 keys | Higher limits |
| Interface | CLI | Web UI + API |
| Database | SQLite (local) | PostgreSQL |

## Common Commands

```bash
# Build the solution
dotnet build UnsecuredAPIKeys.sln

# Run the CLI application
cd UnsecuredAPIKeys.CLI
dotnet run

# Or use the compiled binary
./bin/Debug/net10.0/unsecuredapikeys
```

The CLI presents a menu-driven interface:
1. **Start Scraper** - Search GitHub for exposed keys (runs continuously)
2. **Start Verifier** - Maintain up to 50 valid keys
3. **View Status** - Show current statistics
4. **Configure Settings** - Set GitHub token
5. **Export Keys** - Export to JSON or CSV
6. **Exit**

## Architecture

### Project Structure

```
UnsecuredAPIKeys-OpenSource/
├── UnsecuredAPIKeys.CLI/         # Main CLI application
│   ├── Program.cs                # Entry point with menu UI
│   ├── Constants.cs              # LiteLimits, AppInfo
│   └── Services/
│       ├── ScraperService.cs     # GitHub key discovery
│       ├── VerifierService.cs    # Key validation (50 cap)
│       └── DatabaseService.cs    # SQLite operations
├── UnsecuredAPIKeys.Data/        # EF Core with SQLite
│   ├── DBContext.cs              # Database context
│   ├── Models/                   # Entity models
│   └── Common/                   # Enums, constants
├── UnsecuredAPIKeys.Providers/   # API key validation
│   ├── AI Providers/             # OpenAI, Anthropic, Google
│   └── Search Providers/         # GitHub only
└── unsecuredapikeys.db           # SQLite database (auto-created)
```

### Key Constants

```csharp
// LiteLimits - Intentional restrictions for lite version
public static class LiteLimits
{
    public const int MAX_VALID_KEYS = 50;  // Hard cap
    public const int VERIFICATION_DELAY_MS = 1000;
    public const int SEARCH_DELAY_MS = 5000;
    public const int VERIFICATION_BATCH_SIZE = 10;
}
```

### Provider System

The validation framework uses a registry pattern with attribute-based discovery:

- **`IApiKeyProvider`** (`_Interfaces/IApiKeyProvider.cs`): Core interface defining regex patterns and validation
- **`BaseApiKeyProvider`** (`_Base/BaseApiKeyProvider.cs`): Abstract base with HTTP client management
- **`ApiProviderRegistry`**: Auto-discovers providers via `[ApiProvider]` attribute

**Lite version providers (3 only):**
- `OpenAIProvider` - Patterns: `sk-proj-*`, `sk-or-v1-*`
- `AnthropicProvider` - Pattern: `sk-ant-api*`
- `GoogleProvider` - Pattern: `AIzaSy*`

### Search Provider

- `GitHubSearchProvider` - Uses Octokit to search GitHub code

### Data Models

| Model | Purpose |
|-------|---------|
| `APIKey` | Core entity with key, status, type, timestamps |
| `RepoReference` | Source repository where key was found |
| `SearchQuery` | Search patterns to use |
| `SearchProviderToken` | GitHub token storage |
| `ApplicationSetting` | General settings |

### Key Enums

```csharp
public enum ApiTypeEnum
{
    Unknown = -99,
    OpenAI = 100,
    AnthropicClaude = 120,
    GoogleAI = 130
}

public enum ApiStatusEnum
{
    Unverified = 0,
    Valid = 1,
    Invalid = 2,
    ValidNoCredits = 3,
    Error = 4
}

public enum SearchProviderEnum
{
    Unknown = -99,
    GitHub = 1
}
```

## How It Works

### Scraper Service
1. Retrieves GitHub token from database
2. Gets next search query (rotates through patterns)
3. Searches GitHub using Octokit
4. Extracts potential keys using regex patterns
5. Stores new keys as "Unverified" in SQLite
6. Runs continuously until stopped (Ctrl+C)

### Verifier Service
1. Counts current valid keys in database
2. If at 50 valid keys: re-verifies existing keys (oldest first)
3. If below 50: verifies unverified keys until limit reached
4. When a valid key becomes invalid, verifies new ones
5. Maintains exactly 50 valid keys
6. Runs continuously until stopped (Ctrl+C)

#### Fallback Validation Logic
The verifier uses smart fallback when validating keys:
1. First tries the provider assigned during scraping
2. If validation fails (Unauthorized/HttpError), tries other providers whose regex patterns match the key
3. If a different provider succeeds, reclassifies the key's `ApiType`
4. Only marks key as Invalid if ALL matching providers reject it

This handles cases where a key like `sk-ant-api...` matches OpenAI's broad `sk-*` pattern during scraping but actually belongs to Anthropic.

## Configuration

### First-Time Setup

1. Run the CLI application
2. Go to **Configure Settings** > **Set GitHub Token**
3. Create a token at: https://github.com/settings/tokens
4. Required scope: `public_repo`

### Database

SQLite database is auto-created at `unsecuredapikeys.db` in the working directory. No migrations needed - uses `EnsureCreated()`.

## Security Notice

**WARNING:** Do NOT publish your database or results to a public repository. This would expose working API keys to malicious actors who could abuse them.

## Dependencies

- .NET 10 SDK
- Spectre.Console (rich console UI)
- Entity Framework Core 10 (SQLite)
- Octokit (GitHub API)

## Status Tracking

### Current Status
- Lite version complete
- CLI with menu-driven interface
- GitHub search only
- 3 AI providers: OpenAI, Anthropic, Google
- 50 valid key cap enforced
- SQLite local storage
- Export to JSON/CSV supported
