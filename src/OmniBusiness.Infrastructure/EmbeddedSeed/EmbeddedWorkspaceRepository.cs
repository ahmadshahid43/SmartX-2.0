using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Domain.Foundation;
using OmniBusiness.Infrastructure.Persistence;

namespace OmniBusiness.Infrastructure.EmbeddedSeed;

public sealed class EmbeddedWorkspaceRepository(IHostEnvironment environment) : IWorkspaceRepository
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private WorkspaceSnapshot? _cachedSnapshot;

    public async Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_cachedSnapshot is not null)
        {
            return _cachedSnapshot;
        }

        var seedPath = Path.Combine(environment.ContentRootPath, "Data", "foundation.json");
        await using var stream = File.OpenRead(seedPath);
        var snapshot = await JsonSerializer.DeserializeAsync<WorkspaceSnapshot>(stream, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Unable to load embedded foundation data.");

        _cachedSnapshot = WorkspaceSnapshotNormalization.Normalize(snapshot);
        return _cachedSnapshot;
    }

    public async Task<WorkspaceSnapshot> UpdateWorkspaceSnapshotAsync(
        Func<WorkspaceSnapshot, WorkspaceSnapshot> update,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetWorkspaceSnapshotAsync(cancellationToken);
        _cachedSnapshot = WorkspaceSnapshotNormalization.Normalize(update(snapshot));
        return _cachedSnapshot;
    }

    public async Task<AppUser?> GetUserByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        var snapshot = await GetWorkspaceSnapshotAsync(cancellationToken);
        return MatchLoginIdentifier(snapshot.AdminUser, identifier)
            ? snapshot.AdminUser
            : null;
    }

    public async Task<AppUser?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await GetWorkspaceSnapshotAsync(cancellationToken);
        return snapshot.AdminUser.TenantId == tenantId && snapshot.AdminUser.Id == userId
            ? snapshot.AdminUser
            : null;
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
