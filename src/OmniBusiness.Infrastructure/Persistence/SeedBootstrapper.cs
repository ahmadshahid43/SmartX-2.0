using OmniBusiness.Application.Abstractions.Security;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Infrastructure.Persistence;

/// <summary>
/// Applies first-boot safety overrides to the seed snapshot before it becomes a live workspace.
/// This keeps local development friendly while forcing a custom owner password for public deploys.
/// </summary>
public static class SeedBootstrapper
{
    public static WorkspaceSnapshot Apply(
        WorkspaceSnapshot seedSnapshot,
        PersistenceOptions options,
        IPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(seedSnapshot);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        var normalized = WorkspaceSnapshotNormalization.Normalize(seedSnapshot);
        var users = (normalized.Users ?? Array.Empty<AppUser>()).ToArray();

        if (users.Length == 0)
        {
            users = [normalized.AdminUser];
        }

        var owner = users.FirstOrDefault(user => user.Id == normalized.AdminUser.Id)
            ?? users.FirstOrDefault(IsOwnerRole)
            ?? users[0];
        var ownerEmail = string.IsNullOrWhiteSpace(options.BootstrapOwnerEmail)
            ? owner.Email
            : options.BootstrapOwnerEmail.Trim();
        var ownerDisplayName = string.IsNullOrWhiteSpace(options.BootstrapOwnerDisplayName)
            ? owner.DisplayName
            : options.BootstrapOwnerDisplayName.Trim();
        var ownerPasswordHash = ResolveOwnerPasswordHash(options, passwordHasher, owner);

        var updatedUsers = users
            .Select(user =>
            {
                if (user.Id == owner.Id)
                {
                    return user with
                    {
                        Email = ownerEmail,
                        DisplayName = ownerDisplayName,
                        Role = "Owner",
                        PasswordHash = ownerPasswordHash
                    };
                }

                if (!options.LockNonOwnerSeedUsers)
                {
                    return user;
                }

                return user with { PasswordHash = passwordHasher.HashPassword(CreateOneTimeLockValue()) };
            })
            .ToArray();

        EnsureDistinctEmails(updatedUsers);

        var updatedOwner = updatedUsers.First(user => user.Id == owner.Id);

        return WorkspaceSnapshotNormalization.Normalize(normalized with
        {
            AdminUser = updatedOwner,
            Users = updatedUsers
        });
    }

    private static string ResolveOwnerPasswordHash(
        PersistenceOptions options,
        IPasswordHasher passwordHasher,
        AppUser owner)
    {
        if (string.IsNullOrWhiteSpace(options.BootstrapOwnerPassword))
        {
            if (options.RequireOwnerPasswordOnSeed)
            {
                throw new InvalidOperationException(
                    "Persistence:BootstrapOwnerPassword is required when seeding a production workspace. " +
                    "Supply it via the Persistence__BootstrapOwnerPassword environment variable.");
            }

            return owner.PasswordHash;
        }

        return passwordHasher.HashPassword(options.BootstrapOwnerPassword);
    }

    private static void EnsureDistinctEmails(IReadOnlyList<AppUser> users)
    {
        var duplicate = users
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .GroupBy(user => user.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Seed bootstrap produced duplicate login email '{duplicate.Key}'. " +
                "Update Persistence:BootstrapOwnerEmail or the seed users.");
        }
    }

    private static bool IsOwnerRole(AppUser user) =>
        string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase);

    private static string CreateOneTimeLockValue() => $"locked-{Guid.NewGuid():N}";
}
