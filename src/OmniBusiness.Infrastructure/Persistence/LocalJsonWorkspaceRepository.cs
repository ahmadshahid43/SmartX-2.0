using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Abstractions.Security;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Infrastructure.Persistence;

public sealed class LocalJsonWorkspaceRepository(
    IHostEnvironment environment,
    IOptions<PersistenceOptions> options,
    IPasswordHasher passwordHasher) : IWorkspaceRepository
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PersistenceOptions _options = options.Value;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private WorkspaceSnapshot? _cachedSnapshot;
    private DateTime _cachedLastWriteUtc;

    public async Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var localPath = await EnsureLocalDataFileAsync(cancellationToken);
            var lastWriteUtc = File.GetLastWriteTimeUtc(localPath);

            if (_cachedSnapshot is not null && _cachedLastWriteUtc == lastWriteUtc)
            {
                return _cachedSnapshot;
            }

            var snapshot = await ReadSnapshotCoreAsync(localPath, cancellationToken);

            _cachedSnapshot = snapshot;
            _cachedLastWriteUtc = lastWriteUtc;

            return snapshot;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<WorkspaceSnapshot> UpdateWorkspaceSnapshotAsync(
        Func<WorkspaceSnapshot, WorkspaceSnapshot> update,
        CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var localPath = await EnsureLocalDataFileAsync(cancellationToken);
            var currentSnapshot = await ReadSnapshotCoreAsync(localPath, cancellationToken);
            var updatedSnapshot = WorkspaceSnapshotNormalization.Normalize(update(currentSnapshot));

            await WriteSnapshotCoreAsync(localPath, updatedSnapshot, cancellationToken);

            _cachedSnapshot = updatedSnapshot;
            _cachedLastWriteUtc = File.GetLastWriteTimeUtc(localPath);

            return updatedSnapshot;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<AppUser?> GetUserByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        var snapshot = await GetWorkspaceSnapshotAsync(cancellationToken);
        var users = snapshot.Users ?? Array.Empty<AppUser>();

        return users.FirstOrDefault(user => MatchLoginIdentifier(user, identifier));
    }

    public async Task<AppUser?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await GetWorkspaceSnapshotAsync(cancellationToken);

        return (snapshot.Users ?? Array.Empty<AppUser>())
            .FirstOrDefault(user => user.TenantId == tenantId && user.Id == userId);
    }

    private async Task<string> EnsureLocalDataFileAsync(CancellationToken cancellationToken)
    {
        var localAppDataRoot = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrWhiteSpace(localAppDataRoot))
        {
            localAppDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        var configuredLocalPath = ResolvePath(_options.LocalDataPath);
        var fileName = Path.GetFileName(configuredLocalPath);
        var projectRuntimeRoot = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, "..", "..", ".artifacts", "runtime"));
        var localDirectory = WritableStoragePathResolver.ResolveWritableDirectory(
            environment,
            Path.GetDirectoryName(configuredLocalPath),
            projectRuntimeRoot,
            Path.Combine(Path.GetTempPath(), "SmartX"));
        var localPath = Path.Combine(localDirectory, string.IsNullOrWhiteSpace(fileName) ? "foundation.local.json" : fileName);
        var seedPath = ResolvePath(_options.SeedPath);

        var directoryPath = Path.GetDirectoryName(localPath)
            ?? throw new InvalidOperationException("The configured local data path is invalid.");

        Directory.CreateDirectory(directoryPath);

        if (File.Exists(localPath))
        {
            return localPath;
        }

        if (!_options.InitializeFromSeedOnFirstRun)
        {
            throw new FileNotFoundException(
                $"Workspace data file '{localPath}' was not found and automatic seeding is disabled.");
        }

        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException(
                $"Seed workspace file '{seedPath}' was not found.");
        }

        if (string.Equals(localPath, seedPath, StringComparison.OrdinalIgnoreCase))
        {
            return localPath;
        }

        await using var source = new FileStream(seedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var seedSnapshot = await JsonSerializer.DeserializeAsync<WorkspaceSnapshot>(source, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to load workspace data from '{seedPath}'.");
        var bootstrappedSnapshot = SeedBootstrapper.Apply(seedSnapshot, _options, passwordHasher);

        await WriteSnapshotCoreAsync(localPath, bootstrappedSnapshot, cancellationToken);

        return localPath;
    }

    private async Task<WorkspaceSnapshot> ReadSnapshotCoreAsync(string localPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            localPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        var snapshot = await JsonSerializer.DeserializeAsync<WorkspaceSnapshot>(stream, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to load workspace data from '{localPath}'.");

        return WorkspaceSnapshotNormalization.Normalize(snapshot);
    }

    private async Task WriteSnapshotCoreAsync(
        string localPath,
        WorkspaceSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var tempPath = $"{localPath}.tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, cancellationToken);
        }

        File.Move(tempPath, localPath, true);
    }

    private string ResolvePath(string configuredPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configuredPath);

        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, expanded));
    }

    private static bool MatchLoginIdentifier(AppUser user, string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        var normalizedIdentifier = identifier.Trim();
        var emailLocalPart = user.Email.Split('@', 2)[0];

        return string.Equals(user.Email, normalizedIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(emailLocalPart, normalizedIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.DisplayName, normalizedIdentifier, StringComparison.OrdinalIgnoreCase);
    }
}
