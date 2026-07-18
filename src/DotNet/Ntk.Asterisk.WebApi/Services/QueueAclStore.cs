using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IQueueAclStore
{
    bool RequireLogin { get; }
    int CountEnabled { get; }
    bool IsGateActive { get; }
    IReadOnlyList<QueueAclUserDto> GetList(string? quickSearch = null);
    QueueAclUserDto? GetOne(string id);
    QueueAclUserDto Add(QueueAclUserUpsertRequest request);
    QueueAclUserDto Update(QueueAclUserUpsertRequest request);
    bool Delete(string id);
    void SetRequireLogin(bool requireLogin);
    QueueAclSessionDto? Login(string username, string password);
}

internal sealed class QueueAclUserRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Role { get; set; } = "viewer";
    public List<string> AllowedQueues { get; set; } = new();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class QueueAclDocument
{
    public int Version { get; set; } = 1;
    public bool? RequireLogin { get; set; }
    public List<QueueAclUserRecord> Users { get; set; } = new();
}

public sealed class QueueAclStore : IQueueAclStore
{
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly IOptionsMonitor<QueueAclOptions> _options;
    private readonly IQueueAclSessionService _sessions;
    private readonly ILogger<QueueAclStore> _logger;
    private QueueAclDocument _doc;

    public QueueAclStore(
        IHostEnvironment env,
        IOptionsMonitor<QueueAclOptions> options,
        IQueueAclSessionService sessions,
        ILogger<QueueAclStore> logger)
    {
        _options = options;
        _sessions = sessions;
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "queue-acl-users.json");
        _doc = Load();
    }

    public bool RequireLogin
    {
        get
        {
            lock (_gate)
                return _doc.RequireLogin ?? _options.CurrentValue.RequireLogin;
        }
    }

    public int CountEnabled
    {
        get
        {
            lock (_gate)
                return _doc.Users.Count(u => u.IsEnabled);
        }
    }

    public bool IsGateActive => RequireLogin && CountEnabled > 0;

    public IReadOnlyList<QueueAclUserDto> GetList(string? quickSearch = null)
    {
        lock (_gate)
        {
            IEnumerable<QueueAclUserRecord> q = _doc.Users;
            if (!string.IsNullOrWhiteSpace(quickSearch))
            {
                var s = quickSearch.Trim();
                q = q.Where(u =>
                    u.Username.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || u.Role.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            return q.OrderBy(u => u.Username).Select(ToDto).ToList();
        }
    }

    public QueueAclUserDto? GetOne(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        lock (_gate)
        {
            var u = _doc.Users.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            return u is null ? null : ToDto(u);
        }
    }

    public QueueAclUserDto Add(QueueAclUserUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var username = (request.Username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required.");

        lock (_gate)
        {
            if (_doc.Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Username '{username}' already exists.");

            var now = DateTimeOffset.UtcNow;
            var (hash, salt) = HashPassword(request.Password);
            var record = new QueueAclUserRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsEnabled = request.IsEnabled,
                Role = NormalizeRole(request.Role),
                AllowedQueues = NormalizeQueues(request.AllowedQueues),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _doc.Users.Add(record);
            PersistUnlocked();
            return ToDto(record);
        }
    }

    public QueueAclUserDto Update(QueueAclUserUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new ArgumentException("Id is required.");

        lock (_gate)
        {
            var idx = _doc.Users.FindIndex(x => string.Equals(x.Id, request.Id, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                throw new InvalidOperationException($"User '{request.Id}' not found.");

            var existing = _doc.Users[idx];
            var username = string.IsNullOrWhiteSpace(request.Username)
                ? existing.Username
                : request.Username.Trim();

            if (_doc.Users.Any(u =>
                    !string.Equals(u.Id, existing.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Username '{username}' already exists.");

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                var (hash, salt) = HashPassword(request.Password);
                existing.PasswordHash = hash;
                existing.PasswordSalt = salt;
            }

            existing.Username = username;
            existing.IsEnabled = request.IsEnabled;
            existing.Role = NormalizeRole(request.Role);
            if (request.AllowedQueues is not null)
                existing.AllowedQueues = NormalizeQueues(request.AllowedQueues);
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            PersistUnlocked();
            return ToDto(existing);
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        lock (_gate)
        {
            var removed = _doc.Users.RemoveAll(u => string.Equals(u.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
                return false;
            PersistUnlocked();
            return true;
        }
    }

    public void SetRequireLogin(bool requireLogin)
    {
        lock (_gate)
        {
            _doc.RequireLogin = requireLogin;
            PersistUnlocked();
        }
    }

    public QueueAclSessionDto? Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        QueueAclUserRecord? user;
        lock (_gate)
        {
            user = _doc.Users.FirstOrDefault(u =>
                u.IsEnabled
                && string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
            if (user is null || !VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
                return null;
            user = CloneRecord(user);
        }

        return _sessions.Issue(user.Id, user.Username, user.Role, user.AllowedQueues);
    }

    private QueueAclDocument Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                var empty = new QueueAclDocument
                {
                    RequireLogin = _options.CurrentValue.RequireLogin
                };
                PersistDoc(empty);
                return empty;
            }

            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<QueueAclDocument>(json, JsonOptions) ?? new QueueAclDocument();
            doc.Users ??= new List<QueueAclUserRecord>();
            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load queue ACL store; starting empty");
            return new QueueAclDocument();
        }
    }

    private void PersistUnlocked() => PersistDoc(_doc);

    private void PersistDoc(QueueAclDocument doc)
    {
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        File.WriteAllText(_path, json);
    }

    private static QueueAclUserDto ToDto(QueueAclUserRecord u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        IsEnabled = u.IsEnabled,
        Role = u.Role,
        AllowedQueues = u.AllowedQueues.ToArray(),
        CreatedAtUtc = u.CreatedAtUtc,
        UpdatedAtUtc = u.UpdatedAtUtc
    };

    private static QueueAclUserRecord CloneRecord(QueueAclUserRecord u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        PasswordHash = u.PasswordHash,
        PasswordSalt = u.PasswordSalt,
        IsEnabled = u.IsEnabled,
        Role = u.Role,
        AllowedQueues = u.AllowedQueues.ToList(),
        CreatedAtUtc = u.CreatedAtUtc,
        UpdatedAtUtc = u.UpdatedAtUtc
    };

    private static string NormalizeRole(string? role) =>
        string.Equals(role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "viewer";

    private static List<string> NormalizeQueues(IEnumerable<string>? queues) =>
        (queues ?? Array.Empty<string>())
            .Select(q => q?.Trim() ?? string.Empty)
            .Where(q => q.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    private static bool VerifyPassword(string password, string hashB64, string saltB64)
    {
        try
        {
            var salt = Convert.FromBase64String(saltB64);
            var expected = Convert.FromBase64String(hashB64);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}
