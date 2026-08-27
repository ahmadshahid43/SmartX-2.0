using OmniBusiness.Domain.Foundation;
using OmniBusiness.Infrastructure.Persistence;
using OmniBusiness.Infrastructure.Security;

namespace OmniBusiness.Infrastructure.Tests;

public sealed class SeedBootstrapperTests
{
    private static readonly Guid TenantId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MainBranchId = new("44444444-4444-4444-4444-444444444441");

    [Fact]
    public void Apply_WithBootstrapOwnerPassword_ReplacesOwnerPasswordAndLocksDemoUsers()
    {
        var passwordHasher = new Pbkdf2PasswordHasher();
        var options = new PersistenceOptions
        {
            BootstrapOwnerPassword = "LiveOwner!2026",
            LockNonOwnerSeedUsers = true
        };

        var seeded = BuildSnapshot();
        var bootstrapped = SeedBootstrapper.Apply(seeded, options, passwordHasher);

        Assert.True(passwordHasher.Verify("LiveOwner!2026", bootstrapped.AdminUser.PasswordHash));

        var users = Assert.IsAssignableFrom<IReadOnlyList<AppUser>>(bootstrapped.Users);
        var owner = users.Single(user => user.Id == bootstrapped.AdminUser.Id);
        var ahmad = users.Single(user => user.Email == "ahmad@smartx.local");

        Assert.Equal("Owner", owner.Role);
        Assert.NotEqual(seeded.Users!.Single(user => user.Id == ahmad.Id).PasswordHash, ahmad.PasswordHash);
        Assert.False(passwordHasher.Verify("LiveOwner!2026", ahmad.PasswordHash));
    }

    [Fact]
    public void Apply_WithRequiredOwnerPasswordMissing_Throws()
    {
        var options = new PersistenceOptions
        {
            RequireOwnerPasswordOnSeed = true
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => SeedBootstrapper.Apply(BuildSnapshot(), options, new Pbkdf2PasswordHasher()));

        Assert.Contains("BootstrapOwnerPassword", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_WithBootstrapOwnerEmail_UpdatesOwnerIdentity()
    {
        var passwordHasher = new Pbkdf2PasswordHasher();
        var options = new PersistenceOptions
        {
            BootstrapOwnerEmail = "owner@smartx.pk",
            BootstrapOwnerDisplayName = "SmartX Owner",
            BootstrapOwnerPassword = "Owner@2026"
        };

        var bootstrapped = SeedBootstrapper.Apply(BuildSnapshot(), options, passwordHasher);

        Assert.Equal("owner@smartx.pk", bootstrapped.AdminUser.Email);
        Assert.Equal("SmartX Owner", bootstrapped.AdminUser.DisplayName);
        Assert.True(passwordHasher.Verify("Owner@2026", bootstrapped.AdminUser.PasswordHash));
    }

    private static WorkspaceSnapshot BuildSnapshot()
    {
        var admin = new AppUser(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            TenantId,
            MainBranchId,
            "admin@omnibusiness.local",
            "Admin",
            "Owner",
            "seed-owner-hash");
        var ahmad = new AppUser(
            Guid.Parse("16d3cb0e-15be-4114-a7be-beb22a3d96a6"),
            TenantId,
            MainBranchId,
            "ahmad@smartx.local",
            "Ahmad",
            "Cashier",
            "seed-ahmad-hash");

        return new WorkspaceSnapshot(
            new Tenant(TenantId, "smartx-workspace", "SmartX Workspace", "Configurable ERP + POS", "Premium"),
            new Company(Guid.Parse("22222222-2222-2222-2222-222222222222"), TenantId, "SmartX Workspace", "PKR", "Asia/Karachi", "Pakistan"),
            admin,
            new PosCustomer("Walk-in Customer", "Retail Pricing", "W"),
            new[]
            {
                new Branch(MainBranchId, TenantId, "MAIN", "Main Branch", "Main Warehouse", true)
            },
            Array.Empty<DailyBusinessFigure>(),
            Array.Empty<TrendPoint>(),
            Array.Empty<TopSellingItem>(),
            Array.Empty<BranchPerformance>(),
            Array.Empty<Product>(),
            Array.Empty<SaleRecord>(),
            Array.Empty<CartLine>(),
            new FormDefinition("product-custom-fields", "Product Fields", string.Empty, string.Empty, Array.Empty<FormLibraryField>(), Array.Empty<FormCanvasField>()),
            Users: new[] { admin, ahmad },
            Customers: Array.Empty<CustomerProfile>(),
            StockAdjustments: Array.Empty<StockAdjustmentRecord>(),
            Vendors: Array.Empty<Vendor>(),
            PurchaseOrders: Array.Empty<PurchaseOrder>(),
            StockTransfers: Array.Empty<StockTransfer>(),
            CashShifts: Array.Empty<CashShift>());
    }
}
