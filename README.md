# pgDocker Tools — PostgreSQL Docker Backup Management CLI

A cross-platform CLI tool for managing PostgreSQL backups running in Docker containers.

## Current Status: v0.1 (MVP Development)

### Implemented Features

- ✅ **Backup (Complete)**
  - Automatic detection of PostgreSQL container
  - Version detection
  - Global objects backup (`pg_dumpall --globals-only`)
  - Individual database backups (`pg_dump -Fc`)
  - SHA-256 integrity verification
  - Manifest generation
  - Automatic compression (.zip on Windows, .tar.gz on Linux)


## Quick Start

### Prerequisites

- .NET 10 SDK
- Docker
- PostgreSQL container running

### Build

```bash
dotnet build
```

### Usage

#### Create a Backup

```bash
dotnet run --project src/PgDocker.Cli -- backup
```

With custom config file:

```bash
dotnet run --project src/PgDocker.Cli -- backup -c /path/to/config.yml
```

#### Show Help

```bash
dotnet run --project src/PgDocker.Cli -- help
```

### Configuration

Create a `pgdocker.yml` file:

```yaml
postgres:
  container: postgres          # Docker container name
  username: postgres           # PostgreSQL username

backup:
  path: ./backups             # Where to store backups
  compression: true           # Compress after backup
  verify: true                # Verify integrity
  excludeDatabases:
    - template0
    - template1

retention:
  localDays: 30              # Keep local backups for 30 days
  remoteDays: 90             # Keep remote backups for 90 days

sftp:
  enabled: false             # Enable SFTP (not yet implemented)
  host: backup.example.com
  port: 22
  username: backup
  remotePath: /backups/postgres
  authentication: key
  privateKey: ~/.ssh/backup_ed25519
```

## Backup Structure

Each backup creates a timestamped directory with:

```
20260814_231110/
├── globals.sql              # Global objects (roles, permissions)
├── database1.dump           # Individual database dumps
├── database2.dump
├── manifest.json            # Backup metadata
├── sha256.txt               # SHA-256 checksums
└── (compressed as .zip/.tar.gz)
```

### manifest.json

```json
{
  "ToolVersion": "1.0.0",
  "CreatedAt": "2026-08-14T23:11:11Z",
  "Container": "postgres",
  "PostgresVersion": "17.5",
  "Databases": [
    {
      "Name": "postgres",
      "File": "postgres.dump"
    }
  ]
}
```

## Exit Codes

```
0  - Success
1  - General error
2  - Configuration error
3  - Docker unavailable
4  - PostgreSQL unavailable
5  - Backup failed
6  - Restore failed
7  - Integrity verification failed
8  - SFTP failed
9  - Retention failed
```

## Logging

All operations are logged to console with timestamps:

```
2026-08-14 20:11:08 [INF] Starting backup
2026-08-14 20:11:09 [INF] Docker is available
2026-08-14 20:11:10 [INF] Found 1 databases
2026-08-14 20:11:11 [INF] Backup completed successfully in 3376ms
```

## Development

### Project Structure

```
pgdocker/
├── src/
│   ├── PgDocker.Core/           # Models, interfaces, exit codes
│   ├── PgDocker.Services/       # Service implementations
│   └── PgDocker.Cli/            # CLI entry point
├── tests/
│   └── PgDocker.UnitTests/      # Unit tests
└── pgdocker.yml                 # Configuration template
```

### Running Tests

```bash
dotnet test
```

## Next Steps

1. Implement `restore` command
2. Implement `verify` command with integrity checking
3. Implement SFTP upload/download
4. Implement retention policy cleanup
5. Add integration tests with actual PostgreSQL container
6. Create release binaries for Windows/Linux

## License

MIT
