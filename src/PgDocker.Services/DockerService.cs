namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using System.Diagnostics;

public class DockerService : IDockerService
{
    public async Task<bool> IsDockerAvailableAsync()
    {
        try
        {
            var process = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(process))
            {
                await proc!.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ContainerExistsAsync(string containerName)
    {
        var output = await ExecuteCommandAndGetOutputAsync("docker", new[] { "ps", "-a", "--filter", $"name={containerName}", "--format", "{{.Names}}" });
        return !string.IsNullOrWhiteSpace(output) && output.Contains(containerName);
    }

    public async Task<bool> ContainerIsRunningAsync(string containerName)
    {
        var output = await ExecuteCommandAndGetOutputAsync("docker", new[] { "ps", "--filter", $"name={containerName}", "--format", "{{.Names}}" });
        return !string.IsNullOrWhiteSpace(output) && output.Contains(containerName);
    }

    public async Task<Stream> ExecuteCommandAsync(string containerName, string command, string[] args)
    {
        var allArgs = new List<string> { "exec", containerName, command };
        allArgs.AddRange(args);

        var process = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = string.Join(" ", allArgs.Select(QuoteArgument)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Pass PGPASSWORD from environment if set
        var pgpassword = Environment.GetEnvironmentVariable("PGPASSWORD");
        if (!string.IsNullOrEmpty(pgpassword))
        {
            process.Environment["PGPASSWORD"] = pgpassword;
        }

        var proc = Process.Start(process);
        if (proc == null)
            throw new InvalidOperationException("Failed to start docker process");

        var memoryStream = new MemoryStream();
        await proc.StandardOutput.BaseStream.CopyToAsync(memoryStream);
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Docker command failed: {error}");
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<string> ExecuteCommandAndGetOutputAsync(string containerName, string command, string[] args)
    {
        var allArgs = new List<string> { "exec", containerName, command };
        allArgs.AddRange(args);

        var process = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = string.Join(" ", allArgs.Select(QuoteArgument)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Pass PGPASSWORD from environment if set
        var pgpassword = Environment.GetEnvironmentVariable("PGPASSWORD");
        if (!string.IsNullOrEmpty(pgpassword))
        {
            process.Environment["PGPASSWORD"] = pgpassword;
        }

        using (var proc = Process.Start(process))
        {
            var output = await proc!.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var error = await proc.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Docker command failed: {error}");
            }

            return output.Trim();
        }
    }

    public async Task<string> ExecuteCommandWithInputAsync(string containerName, string command, string[] args, Stream inputStream)
    {
        var allArgs = new List<string> { "exec", "-i", containerName, command };
        allArgs.AddRange(args);

        var process = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = string.Join(" ", allArgs.Select(QuoteArgument)),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Pass PGPASSWORD from environment if set
        var pgpassword = Environment.GetEnvironmentVariable("PGPASSWORD");
        if (!string.IsNullOrEmpty(pgpassword))
        {
            process.Environment["PGPASSWORD"] = pgpassword;
        }

        using (var proc = Process.Start(process))
        {
            // Write input to stdin
            await inputStream.CopyToAsync(proc!.StandardInput.BaseStream);
            proc.StandardInput.Close();

            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var error = await proc.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Docker command failed: {error}");
            }

            return output.Trim();
        }
    }

    private async Task<string> ExecuteCommandAndGetOutputAsync(string command, string[] args)
    {
        var process = new ProcessStartInfo
        {
            FileName = command,
            Arguments = string.Join(" ", args.Select(QuoteArgument)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var proc = Process.Start(process))
        {
            var output = await proc!.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Trim();
        }
    }

    private string QuoteArgument(string arg)
    {
        if (arg.Contains("\""))
            return "'" + arg + "'";
        if (arg.Contains(" "))
            return "\"" + arg + "\"";
        return arg;
    }
}
