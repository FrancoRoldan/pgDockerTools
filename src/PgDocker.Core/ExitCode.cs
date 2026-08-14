namespace PgDocker.Core;

public enum ExitCode
{
    Success = 0,
    GeneralError = 1,
    ConfigurationError = 2,
    DockerUnavailable = 3,
    PostgresUnavailable = 4,
    BackupFailed = 5,
    RestoreFailed = 6,
    IntegrityVerificationFailed = 7,
    SftpFailed = 8,
    RetentionFailed = 9
}
