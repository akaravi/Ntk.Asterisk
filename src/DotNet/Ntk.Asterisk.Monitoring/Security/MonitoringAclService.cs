using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Core.Contracts;
using Ntk.Asterisk.Domain.Recording;

namespace Ntk.Asterisk.Monitoring.Security;

public interface IMonitoringAclService
{
    Task<bool> CanSpyChannelAsync(string? username, string targetExtensionOrChannel, CancellationToken cancellationToken = default);
    Task<bool> CanRecordChannelAsync(string? username, string targetExtensionOrChannel, CancellationToken cancellationToken = default);
    Task<bool> CanInterveneAsync(string? username, string targetExtensionOrChannel, CancellationToken cancellationToken = default);
    Task<bool> CanAccessRecordingAsync(string? username, AudioRecording recording, CancellationToken cancellationToken = default);
    Task<QueueAclUserDto?> GetUserAsync(string? username, CancellationToken cancellationToken = default);
}

public class MonitoringAclService : IMonitoringAclService
{
    private readonly IOptionsMonitor<MonitoringOptions> _monitoringOptions;
    private readonly IOptionsMonitor<QueueAclOptions> _aclOptions;
    private readonly ILogger<MonitoringAclService> _logger;

    public MonitoringAclService(
        IOptionsMonitor<MonitoringOptions> monitoringOptions,
        IOptionsMonitor<QueueAclOptions> aclOptions,
        ILogger<MonitoringAclService> logger)
    {
        _monitoringOptions = monitoringOptions;
        _aclOptions = aclOptions;
        _logger = logger;
    }

    private async Task<List<QueueAclUserDto>> LoadUsersAsync(CancellationToken cancellationToken)
    {
        var filePath = _aclOptions.CurrentValue.UsersFilePath;
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, filePath);
        }

        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var users = JsonSerializer.Deserialize<List<QueueAclUserDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return users ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Queue ACL users file at {FilePath}", filePath);
            return [];
        }
    }

    public async Task<QueueAclUserDto?> GetUserAsync(string? username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var users = await LoadUsersAsync(cancellationToken);
        return users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> CanSpyChannelAsync(string? username, string targetExtensionOrChannel, CancellationToken cancellationToken = default)
    {
        if (!_monitoringOptions.CurrentValue.AclEnabled || !_aclOptions.CurrentValue.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Anonymous user rejected from spying channel {Channel}", targetExtensionOrChannel);
            return false;
        }

        var user = await GetUserAsync(username, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User '{Username}' not found in ACL store. Spying rejected.", username);
            return false;
        }

        if (user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!user.CanSpy)
        {
            _logger.LogWarning("User '{Username}' does not have CanSpy permission.", username);
            return false;
        }

        return true;
    }

    public async Task<bool> CanRecordChannelAsync(string? username, string targetExtensionOrChannel, CancellationToken cancellationToken = default)
    {
        if (!_monitoringOptions.CurrentValue.AclEnabled || !_aclOptions.CurrentValue.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Anonymous user rejected from recording channel {Channel}", targetExtensionOrChannel);
            return false;
        }

        var user = await GetUserAsync(username, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User '{Username}' not found in ACL store. Recording rejected.", username);
            return false;
        }

        if (user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!user.CanRecord)
        {
            _logger.LogWarning("User '{Username}' does not have CanRecord permission.", username);
            return false;
        }

        return true;
    }

    public async Task<bool> CanInterveneAsync(string? username, string targetExtensionOrChannel, CancellationToken cancellationToken = default)
    {
        if (!_monitoringOptions.CurrentValue.AclEnabled || !_aclOptions.CurrentValue.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Anonymous user rejected from intervening on channel {Channel}", targetExtensionOrChannel);
            return false;
        }

        var user = await GetUserAsync(username, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User '{Username}' not found in ACL store. Intervention rejected.", username);
            return false;
        }

        // Only admin or manager roles can perform destructive intervention actions (Hangup/Bridge)
        return user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
               user.Role.Equals("manager", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> CanAccessRecordingAsync(string? username, AudioRecording recording, CancellationToken cancellationToken = default)
    {
        if (!_monitoringOptions.CurrentValue.AclEnabled || !_aclOptions.CurrentValue.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Anonymous user rejected from accessing recording {Id}", recording.Id);
            return false;
        }

        var user = await GetUserAsync(username, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User '{Username}' not found in ACL store. Recording access rejected.", username);
            return false;
        }

        if (user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return true;

        // User can access if they have recording permission or it's their own extension
        if (!string.IsNullOrEmpty(user.Username) && (user.Username.Equals(recording.Extension, StringComparison.OrdinalIgnoreCase) || user.Username.Equals(recording.CallerIdNum, StringComparison.OrdinalIgnoreCase)))
            return true;

        return user.CanRecord;
    }
}
