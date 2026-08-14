# pgDocker Tools — PostgreSQL Docker Backup Management CLI

A cross-platform CLI tool for managing PostgreSQL backups running in Docker containers.

## Current Status: v0.1 (Active Development)

### Implemented Features

- ✅ **Backup**
  - Automatic detection of PostgreSQL container
  - Version detection
  - Global objects backup (`pg_dumpall --globals-only`)
  - Individual database backups (`pg_dump -Fc`)
  - SHA-256 integrity verification
  - Manifest generation
  - Automatic compression (.zip on Windows, .tar.gz on Linux)
  - Optional SFTP upload after backup
  - Optional retention policy application

- ✅ **Restore**
  - Restore from latest or specific backup
  - Selective database restore
  - Optional database cleanup before restore
  - Confirmation prompts for destructive operations

- ✅ **Verify**
  - Integrity verification using SHA-256 checksums
  - Backup format validation
  - Manifest validation

- ✅ **List**
  - View all available backups
  - Display backup metadata (creation date, size, compression status)

- ✅ **Info**
  - Detailed backup information
  - Database list within backup
  - Tool version and PostgreSQL version used

- ✅ **Prune**
  - Retention policy enforcement
  - Dry-run mode for safe testing
  - Automatic cleanup of old backups

### Upcoming Features

- 🔄 **SFTP Upload/Download** (stub implementation)
- 🔄 **Scheduled backups** (via cron/Task Scheduler)

## Quick Start

### Prerequisites

- .NET 10.0 SDK
- Docker
- PostgreSQL container running

### Build

```bash
dotnet build
```

### Run

All commands follow this pattern:
```bash
dotnet run --project src/PgDocker.Cli -- [command] [options]
```

### Available Commands

```
backup             Create a backup of PostgreSQL databases
restore            Restore PostgreSQL databases from a backup
verify             Verify the integrity of a backup
list               List available backups
info               Show information about a backup
upload             Upload a backup to SFTP server (not yet implemented)
download           Download a backup from SFTP server (not yet implemented)
prune              Remove old backups according to retention policy
help               Show help message
```

### Common Usage Examples

#### Create a Backup

```bash
dotnet run --project src/PgDocker.Cli -- backup
```

With custom config and automatic upload/prune:
```bash
dotnet run --project src/PgDocker.Cli -- backup -c config.yml -u -p
```

Options:
- `-c, --config` - Path to configuration file (default: pgdocker.yml)
- `-u, --upload` - Upload backup to SFTP after completion
- `-p, --prune` - Apply retention policy after backup

#### Restore from Backup

```bash
# Restore from latest backup
dotnet run --project src/PgDocker.Cli -- restore

# Restore specific database
dotnet run --project src/PgDocker.Cli -- restore -d mydb

# Clean (drop) database before restore
dotnet run --project src/PgDocker.Cli -- restore -d mydb --clean

# Restore from specific backup (skip confirmation)
dotnet run --project src/PgDocker.Cli -- restore backup_20260814_231110 -y
```

Options:
- `-c, --config` - Path to configuration file
- `-d, --database` - Specific database to restore
- `--clean` - Drop database before restore
- `-y, --yes` - Skip confirmation prompts

#### Verify Backup

```bash
# Verify latest backup
dotnet run --project src/PgDocker.Cli -- verify

# Verify specific backup
dotnet run --project src/PgDocker.Cli -- verify backup_20260814_231110
```

#### List Backups

```bash
dotnet run --project src/PgDocker.Cli -- list
```

#### Show Backup Info

```bash
# Info for latest backup
dotnet run --project src/PgDocker.Cli -- info

# Info for specific backup
dotnet run --project src/PgDocker.Cli -- info backup_20260814_231110
```

#### Apply Retention Policy

```bash
# Dry-run: show what would be deleted
dotnet run --project src/PgDocker.Cli -- prune --dry-run

# Actually delete old backups
dotnet run --project src/PgDocker.Cli -- prune
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

1. Implement SFTP upload/download operations
2. Add integration tests with actual PostgreSQL container
3. Add verbose/quiet logging modes
4. Create release binaries for Windows/Linux
5. Add support for scheduled backups via cron/Task Scheduler
6. Performance optimizations for large databases

## License

MIT
