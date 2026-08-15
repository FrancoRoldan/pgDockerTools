# SFTP Setup Guide

This guide shows how to configure and use SFTP backup upload/download with pgDockerTools.

## Prerequisites

- An SFTP server with SSH key authentication
- SSH private key (Ed25519 or RSA format)
- SFTP user account on remote server

## Configuration

### 1. Generate SSH Keys (if needed)

On Windows, you can use:
```powershell
# Using PuTTYgen or OpenSSH
ssh-keygen -t ed25519 -f ~/.ssh/backup_ed25519 -N ""
```

### 2. Add Public Key to SFTP Server

Copy your public key to the SFTP server:
```bash
cat ~/.ssh/backup_ed25519.pub >> ~/.ssh/authorized_keys
```

### 3. Configure pgdocker.yml

#### Option A: SSH Key Authentication (Recommended)

```yaml
postgres:
  container: postgres
  username: postgres

backup:
  path: ./backups
  compression: true
  verify: true
  excludeDatabases:
    - template0
    - template1

retention:
  localDays: 30
  remoteDays: 90

sftp:
  enabled: true                          # Enable SFTP
  host: backup.example.com              # SFTP server hostname
  port: 22                               # SFTP port (default: 22)
  username: backup                       # SFTP username
  remotePath: /backups/postgres          # Remote directory for backups
  authentication: key                    # Authentication method: "key" or "password"
  privateKey: ~/.ssh/backup_ed25519     # Path to private key (~/ expands to home)
  password: ""                           # Leave empty for key auth
```

#### Option B: Password Authentication

```yaml
sftp:
  enabled: true
  host: backup.example.com
  port: 22
  username: backup
  remotePath: /backups/postgres
  authentication: password               # Use password authentication
  privateKey: ""                         # Leave empty for password auth
  password: "your_sftp_password"        # SFTP password (consider using secure storage)
```

## Usage Examples

### Upload Latest Backup

```bash
pgdocker upload
```

This uploads:
- Latest compressed backup archive (.zip or .tar.gz)
- manifest.json (backup metadata)
- sha256.txt (integrity checksums)

### Upload Specific Backup

```bash
pgdocker upload backup_20260814_231110
```

### Auto-Upload After Backup

```bash
pgdocker backup -u
```

The `-u` flag automatically uploads after backup completes.

### Download Backup from Remote

```bash
pgdocker download backup_20260814_231110
```

Downloads to local `./backups/` directory.

## Directory Structure

### Local Backups
```
./backups/
├── backup_20260814_231110/
│   ├── globals.sql
│   ├── database1.dump
│   ├── manifest.json
│   ├── sha256.txt
│   └── backup_20260814_231110.zip
```

### Remote SFTP Path
```
/backups/postgres/
├── backup_20260814_231110.zip
├── backup_20260815_120000.zip
└── backup_20260814_231110/
    ├── manifest.json
    └── sha256.txt
```

## Security Considerations

### SSH Key Authentication (Recommended ⭐)

1. **Private Key Permissions**: Ensure private key has correct permissions:
   ```bash
   chmod 600 ~/.ssh/backup_ed25519
   ```

2. **SSH Key Type**: Ed25519 keys are recommended (more secure, smaller):
   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/backup_ed25519
   ```

3. **Key Passphrase**: Optionally protect key with passphrase:
   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/backup_ed25519 -N "passphrase"
   ```

### Password Authentication ⚠️

1. **Avoid Plain Text**: Never hardcode passwords in pgdocker.yml
   
2. **Alternatives for Passwords**:
   - Use SSH keys (recommended)
   - Store password in environment variable:
     ```yaml
     password: ${SFTP_PASSWORD}  # Not yet supported, set manually
     ```
   - Use Windows Credential Manager or similar secure storage

3. **Config File Permissions**: Always protect configuration file:
   ```bash
   chmod 600 pgdocker.yml
   ```

### General Best Practices

1. **Remote Directory**: Create dedicated user on SFTP server with limited permissions:
   ```bash
   # On SFTP server as root
   useradd -m -s /bin/false backup
   mkdir -p /backups/postgres
   chown backup:backup /backups/postgres
   chmod 700 /backups/postgres
   ```

2. **Firewall**: Restrict SFTP access to specific IPs:
   ```bash
   # On server: only allow from backup server IP
   iptables -A INPUT -p tcp --dport 22 -s 192.168.1.100 -j ACCEPT
   ```

3. **Monitoring**: Enable SSH key logging:
   ```bash
   # On server: log failed attempts
   auditctl -w /var/log/auth.log -p wa -k sshd_log_changes
   ```

## Troubleshooting

### Connection Failed
- Verify SFTP server is running and accessible
- Check firewall rules allow port 22 (or configured port)
- Verify SSH key is in authorized_keys on server

### Permission Denied
- Ensure private key path is correct (~/ expands to home directory)
- Verify key permissions: `chmod 600 ~/.ssh/backup_ed25519`
- Check SFTP user has write permissions on remote directory

### Directory Creation Failed
- Verify SFTP user owns parent directory on server
- Check remote path syntax (use forward slashes: `/backups/postgres`)

## Retention Policy with SFTP

The `remoteDays` setting in retention config controls how long remote backups are kept:

```yaml
retention:
  localDays: 30      # Keep local backups 30 days
  remoteDays: 90     # Keep remote backups 90 days
```

Prune old remote backups:
```bash
pgdocker prune        # Show what would be deleted
pgdocker prune --dry-run  # Dry-run mode
```

## Integration with Backup Workflow

Complete backup with upload and prune:
```bash
pgdocker backup -u -p -c pgdocker.yml
```

This:
1. Creates backup
2. Compresses if configured
3. Uploads to SFTP (`-u`)
4. Applies retention policy (`-p`)

## SSH Key Formats

SSH.NET supports these key formats:
- OpenSSH format (Ed25519, RSA)
- PuTTY format (.ppk)

To use a key generated with PuTTYgen:
```yaml
privateKey: ~/.ssh/backup_key.ppk
```

## Next Steps

- Set up automated backups with Windows Task Scheduler (planned feature)
- Monitor SFTP upload logs with `--verbose` flag:
  ```bash
  pgdocker upload --verbose
  ```
- Test recovery procedure regularly
