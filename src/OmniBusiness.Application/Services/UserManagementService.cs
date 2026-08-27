using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Abstractions.Security;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

public sealed class UserManagementService(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceQueryService workspaceQueryService,
    IPasswordHasher passwordHasher) : IUserManagementService
{
    public async Task<WorkspaceUsersDto> CreateUserAsync(
        Guid tenantId,
        SaveWorkspaceUserRequestDto request,
        CancellationToken cancellationToken)
    {
        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);
            var branch = ResolveBranch(snapshot, request.BranchId);
            var users = (snapshot.Users ?? Array.Empty<AppUser>()).ToList();
            var email = NormalizeEmail(request.Email);
            var displayName = NormalizeDisplayName(request.DisplayName, email);
            var role = NormalizeRole(request.Role);

            if (users.Any(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }

            var password = NormalizePassword(request.Password, isRequired: true);
            users.Add(new AppUser(
                Guid.NewGuid(),
                tenantId,
                branch.Id,
                email,
                displayName,
                role,
                passwordHasher.HashPassword(password)));

            return snapshot with { Users = users.ToArray() };
        }, cancellationToken);

        return await workspaceQueryService.GetUsersAsync(tenantId, cancellationToken);
    }

    public async Task<WorkspaceUsersDto> UpdateUserAsync(
        Guid tenantId,
        Guid userId,
        SaveWorkspaceUserRequestDto request,
        CancellationToken cancellationToken)
    {
        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);
            var branch = ResolveBranch(snapshot, request.BranchId);
            var users = (snapshot.Users ?? Array.Empty<AppUser>()).ToList();
            var index = users.FindIndex(user => user.Id == userId);

            if (index < 0)
            {
                throw new InvalidOperationException("The selected user could not be found.");
            }

            var existing = users[index];
            var email = NormalizeEmail(request.Email);
            var displayName = NormalizeDisplayName(request.DisplayName, email);
            var role = NormalizeRole(request.Role);

            if (snapshot.AdminUser.Id == userId && !string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The primary owner must remain in the Owner role.");
            }

            if (users.Any(user => user.Id != userId && string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }

            var passwordHash = string.IsNullOrWhiteSpace(request.Password)
                ? existing.PasswordHash
                : passwordHasher.HashPassword(NormalizePassword(request.Password, isRequired: false));

            var updatedUser = existing with
            {
                BranchId = branch.Id,
                Email = email,
                DisplayName = displayName,
                Role = role,
                PasswordHash = passwordHash
            };

            users[index] = updatedUser;

            return snapshot with
            {
                AdminUser = snapshot.AdminUser.Id == userId ? updatedUser : snapshot.AdminUser,
                Users = users.ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetUsersAsync(tenantId, cancellationToken);
    }

    private static Branch ResolveBranch(WorkspaceSnapshot snapshot, Guid branchId)
    {
        return snapshot.Branches.FirstOrDefault(branch => branch.Id == branchId)
            ?? throw new InvalidOperationException("Select a valid branch for the user.");
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizeDisplayName(string displayName, string email)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? email
            : displayName.Trim();
    }

    private static string NormalizeRole(string role)
    {
        return string.IsNullOrWhiteSpace(role)
            ? "Cashier"
            : role.Trim();
    }

    private static string NormalizePassword(string? password, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            if (isRequired)
            {
                throw new InvalidOperationException("Password is required for a new user.");
            }

            throw new InvalidOperationException("Password could not be processed.");
        }

        var trimmed = password.Trim();
        if (trimmed.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters long.");
        }

        return trimmed;
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }
}
