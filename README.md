# UnsecuredAPIKeys Lite

[![GitHub Stars](https://img.shields.io/github/stars/TSCarterJr/UnsecuredAPIKeys-OpenSource?style=social)](https://github.com/TSCarterJr/UnsecuredAPIKeys-OpenSource)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Custom-blue)](LICENSE)


## ⚠️ Educational Purpose Only
> **Thank you to everyone who has starred this project!** Your support helps raise awareness about API key security and encourages responsible disclosure practices.

> **Full Version Available:** [www.UnsecuredAPIKeys.com](https://www.UnsecuredAPIKeys.com)
>
> The full version offers: Web UI, all API providers, community features, and more.

A command-line tool for discovering and validating exposed API keys on GitHub. This lite version focuses on educational and security awareness purposes.

## Lite Version Limits

| Feature | Lite (This Repo) | Full Version |
|---------|------------------|--------------|
| Search Provider | GitHub only | GitHub, GitLab, SourceGraph |
| API Providers | OpenAI, Anthropic, Google | 15+ providers |
| Valid Key Cap | 50 keys | Higher limits |
| Interface | CLI | Web UI + API |
| Database | SQLite (local) | PostgreSQL |

## Security Warning

**Do NOT publish your database or results to a public repository.** This would expose working API keys to malicious actors who could abuse them.

## Quick Start

### 1. Clone & Build

```bash
git clone https://github.com/TSCarterJr/UnsecuredAPIKeys-OpenSource.git
cd UnsecuredAPIKeys-OpenSource
dotnet build
```

### 2. Run the Application

```bash
cd UnsecuredAPIKeys.CLI
dotnet run
```

Or use the compiled binary:

```bash
./bin/Debug/net9.0/unsecuredapikeys
```

### 3. Configure GitHub Token

On first run, go to **Configure Settings** > **Set GitHub Token**.

Create a token at: https://github.com/settings/tokens
Required scope: `public_repo`

### 4. Start Searching

- **Start Scraper**: Searches GitHub for exposed API keys (runs continuously)
- **Start Verifier**: Maintains up to 50 valid keys (re-checks as needed)
- **View Status**: Shows current statistics
- **Export Keys**: Export to JSON or CSV

## How It Works

### Scraper
1. Uses your GitHub token to search for common API key patterns
2. Extracts potential keys using regex patterns for OpenAI, Anthropic, and Google
3. Stores discovered keys in a local SQLite database

### Verifier
1. Validates discovered keys against the actual provider APIs
2. Maintains exactly 50 valid keys (lite limit)
3. Re-checks existing valid keys periodically
4. When a key becomes invalid, verifies new ones until back to 50

## Project Structure

```
UnsecuredAPIKeys-OpenSource/
├── UnsecuredAPIKeys.CLI/         # Main CLI application
├── UnsecuredAPIKeys.Data/        # SQLite database layer
├── UnsecuredAPIKeys.Providers/   # API validation providers
├── unsecuredapikeys.db           # SQLite database (auto-created)
└── README.md
```

## Prerequisites

- **.NET 9 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
- **GitHub Personal Access Token** - [Create here](https://github.com/settings/tokens)
- **Platform**: Windows, macOS, or Linux

## Supported Providers (Lite)

| Provider | Pattern Examples |
|----------|------------------|
| OpenAI | `sk-proj-*`, `sk-or-v1-*` |
| Anthropic | `sk-ant-api*` |
| Google AI | `AIzaSy*` |

## Configuration

Copy `appsettings.example.json` to `appsettings.json` and configure:

```json
{
  "GitHub": {
    "Token": "ghp_YOUR_TOKEN"
  },
  "Database": {
    "Path": "unsecuredapikeys.db"
  }
}
```

Or configure directly via the CLI menu.

## Database

The SQLite database (`unsecuredapikeys.db`) is auto-created on first run in the working directory.

| Action | How |
|--------|-----|
| **Location** | Same folder as the executable |
| **Reset** | Delete `unsecuredapikeys.db` and restart |
| **Backup** | Copy the `.db` file |
| **View data** | Use any SQLite browser (e.g., DB Browser for SQLite) |

## Search Queries

On first run, default search queries are automatically seeded:

- `sk-proj-`, `sk-or-v1-`, `OPENAI_API_KEY` (OpenAI)
- `sk-ant-api`, `ANTHROPIC_API_KEY` (Anthropic)
- `AIzaSy`, `GOOGLE_API_KEY` (Google)

The scraper rotates through these queries automatically.

## Rate Limiting

Built-in delays prevent API abuse:

| Operation | Delay |
|-----------|-------|
| Between searches | 5 seconds |
| Between verifications | 1 second |
| Batch size | 10 keys |

GitHub's API allows ~30 searches/minute with authentication.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "No GitHub token configured" | Go to Configure Settings > Set GitHub Token |
| "Rate limit exceeded" | Wait 60 seconds, or use a different token |
| Build fails | Ensure .NET 9 SDK is installed: `dotnet --version` |
| No keys found | Check your token has `public_repo` scope |
| Database locked | Close other apps using the .db file |

## Legal & Ethical Use

- **Educational Purpose**: This tool demonstrates API security vulnerabilities
- **Responsible Use**: Only use for legitimate security research
- **No Abuse**: Do not use discovered keys for unauthorized access
- **Compliance**: Follow all applicable laws and terms of service

## License

This project uses a **custom attribution-required license** based on MIT.

### Attribution Required

Any use of this code requires visible attribution:
- Display: "Based on UnsecuredAPIKeys Open Source"
- Link to: https://github.com/TSCarterJr/UnsecuredAPIKeys-OpenSource
- Must be visible in UI/documentation

See [LICENSE](LICENSE) for full details.

## Legacy UI Version

Looking for the original Web UI + WebAPI architecture? Check the [`legacy_ui`](https://github.com/TSCarterJr/UnsecuredAPIKeys-OpenSource/tree/legacy_ui) branch.

> **Note**: The legacy branch is no longer actively maintained. For the full-featured web experience, use [www.UnsecuredAPIKeys.com](https://www.UnsecuredAPIKeys.com).

## Full Version

For higher limits, more providers, web interface, and community features:

**[www.UnsecuredAPIKeys.com](https://www.UnsecuredAPIKeys.com)**

---

**Remember**: Use responsibly and in accordance with applicable laws.
