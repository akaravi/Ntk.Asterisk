using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Domain.Recording;

namespace Ntk.Asterisk.Monitoring.Storage;

public class FileSystemAudioRecordingStorage : IAudioRecordingStorage
{
    private readonly IOptionsMonitor<RecordingOptions> _options;
    private readonly ILogger<FileSystemAudioRecordingStorage> _logger;

    public FileSystemAudioRecordingStorage(
        IOptionsMonitor<RecordingOptions> options,
        ILogger<FileSystemAudioRecordingStorage> logger)
    {
        _options = options;
        _logger = logger;
    }

    private string GetBaseDirectory()
    {
        var basePath = _options.CurrentValue.FileSystem.BasePath;
        if (!Path.IsPathRooted(basePath))
        {
            basePath = Path.Combine(AppContext.BaseDirectory, basePath);
        }
        var canonicalBase = Path.GetFullPath(basePath);
        if (!Directory.Exists(canonicalBase))
        {
            Directory.CreateDirectory(canonicalBase);
        }
        return canonicalBase;
    }

    public string GetFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be null or empty.", nameof(relativePath));

        var baseDir = GetBaseDirectory();
        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var combined = Path.Combine(baseDir, normalizedRelative);
        var canonicalCandidate = Path.GetFullPath(combined);

        // Security check: candidate must be strictly within baseDir
        var basePrefix = baseDir.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? baseDir
            : baseDir + Path.DirectorySeparatorChar;

        if (!canonicalCandidate.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(canonicalCandidate, baseDir, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected: {RelativePath}", relativePath);
            throw new UnauthorizedAccessException($"Invalid path traversal: '{relativePath}' escapes base directory.");
        }

        return canonicalCandidate;
    }

    public async Task<string> SaveAsync(string relativePath, Stream audioStream, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await audioStream.CopyToAsync(fileStream, cancellationToken);
        _logger.LogInformation("Saved audio recording to {FullPath}", fullPath);
        return fullPath;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Audio recording file not found: {FullPath}", fullPath);
            return Task.FromResult<Stream?>(null);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted recording file: {FullPath}", fullPath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete recording file {FullPath}", fullPath);
                return Task.FromResult(false);
            }
        }
        return Task.FromResult(false);
    }

    public Task<long> GetFileSizeAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            return Task.FromResult(new FileInfo(fullPath).Length);
        }
        return Task.FromResult(0L);
    }
}
