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

- ✅ **SFTP Upload/Download**
  - SSH key-based authentication
  - Upload/download backups to/from remote SFTP servers
  - Automatic metadata sync (manifest, checksums)

### Upcoming Features

- 🔄 **Scheduled backups** (via cron/Task Scheduler)
- 🔄 **Password-based SFTP authentication**

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
upload             Upload a backup to SFTP server
download           Download a backup from SFTP server
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

#### Upload Backup to SFTP

```bash
# Upload latest backup
dotnet run --project src/PgDocker.Cli -- upload

# Upload specific backup
dotnet run --project src/PgDocker.Cli -- upload backup_20260814_231110

# Upload with custom config file
dotnet run --project src/PgDocker.Cli -- upload -c config.yml
```

Uploads backup archive, manifest, and checksums to remote SFTP server. Requires SFTP to be enabled in configuration.

#### Download Backup from SFTP

```bash
# Download specific backup
dotnet run --project src/PgDocker.Cli -- download backup_20260814_231110

# Download with custom config file
dotnet run --project src/PgDocker.Cli -- download backup_20260814_231110 -c config.yml
```

Downloads backup archive, manifest, and checksums from remote SFTP server. Requires SFTP to be enabled in configuration.

#### Apply Retention Policy

```bash
# Dry-run: show what would be deleted
dotnet run --project src/PgDocker.Cli -- prune --dry-run

# Actually delete old backups
dotnet run --project src/PgDocker.Cli -- prune
```

#### Logging Options

All commands support verbosity control:

```bash
# Show detailed debug logs
dotnet run --project src/PgDocker.Cli -- backup --verbose

# Suppress non-critical logs (warnings/errors only)
dotnet run --project src/PgDocker.Cli -- backup --quiet
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
# Run all tests (including integration tests that require Docker)
dotnet test

# Run only fast unit tests (skip integration tests)
dotnet test --filter Category!=Integration
```

**Integration tests** require Docker to be running. They spin up a real PostgreSQL container, create backups, verify integrity, and test restore operations end-to-end. If Docker is not available, skip them with the `--filter` command above.

### Publishing Release Binaries

To create self-contained single-file executables for Windows and Linux:

```bash
# Windows x64
dotnet publish src/PgDocker.Cli -c Release -r win-x64 -o publish/win-x64

# Linux x64
dotnet publish src/PgDocker.Cli -c Release -r linux-x64 -o publish/linux-x64
```

The resulting binaries (`publish/win-x64/PgDocker.Cli.exe` and `publish/linux-x64/PgDocker.Cli`) require no .NET runtime to be installed on the target system.

## Next Steps

1. Add password-based SFTP authentication (in addition to key-based)
2. Add integration tests with SFTP server for upload/download
3. Create release binaries for Windows/Linux/Mac
4. Add support for scheduled backups via cron/Task Scheduler
5. Performance optimizations for large databases
6. Add incremental backup support

## License

MIT
