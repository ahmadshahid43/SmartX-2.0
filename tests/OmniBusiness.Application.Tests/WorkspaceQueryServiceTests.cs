using System.Text;
using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Tests;

public sealed class WorkspaceQueryServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ComputesDeltaAgainstPreviousDay()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = new WorkspaceSnapshot(
            new Tenant(tenantId, "demo-retail", "Demo Retail Company", "General Retail", "Business"),
            new Company(Guid.NewGuid(), tenantId, "Demo Retail Company", "PKR", "Asia/Karachi", "Pakistan"),
            new AppUser(Guid.NewGuid(), tenantId, Guid.NewGuid(), "admin@omnibusiness.local", "Admin", "Owner", "x:y"),
            new PosCustomer("Walk-in Customer", "Retail Pricing", "W"),
            Array.Empty<Branch>(),
            new[]
            {
                new DailyBusinessFigure(new DateOnly(2026, 8, 22), 100m, 75m, 50m),
                new DailyBusinessFigure(new DateOnly(2026, 8, 23), 125m, 45m, 80m)
            },
            Array.Empty<TrendPoint>(),
            Array.Empty<TopSellingItem>(),
            Array.Empty<BranchPerformance>(),
            Array.Empty<Product>(),
            Array.Empty<SaleRecord>(),
            Array.Empty<CartLine>(),
            new FormDefinition("form", "Product Custom Fields", "Desc", "field", Array.Empty<FormLibraryField>(), Array.Empty<FormCanvasField>()));

        var service = new WorkspaceQueryService(new StubWorkspaceRepository(snapshot));

        var dashboard = await service.GetDashboardAsync(tenantId, CancellationToken.None);

        Assert.Equal(25m, dashboard.Sales.DeltaPercentage);
        Assert.Equal("up", dashboard.Sales.DeltaDirection);
        Assert.Equal(125m, dashboard.Sales.Value);
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsZeroMetricsWhenNoDailyFiguresConfigured()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = BuildSnapshot(tenantId, dailyFigures: Array.Empty<DailyBusinessFigure>());

        var service = new WorkspaceQueryService(new StubWorkspaceRepository(snapshot));

        var dashboard = await service.GetDashboardAsync(tenantId, CancellationToken.None);

        Assert.Equal(0m, dashboard.Sales.Value);
        Assert.Equal(0m, dashboard.Purchases.Value);
        Assert.Equal(0m, dashboard.GrossProfit.Value);
        Assert.Equal(0m, dashboard.Sales.DeltaPercentage);
    }

    [Fact]
    public async Task GetCustomerHubAsync_ComputesLifetimeValueAndLoyaltyMetrics()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var customer = new CustomerProfile(
            Guid.Parse("77777777-7777-7777-7777-777777777772"),
            tenantId,
            "Ayesha Malik",
            "Retail Pricing",
            "A",
            "+92 300 1112233",
            false,
            "ayesha@demo-retail.pk",
            "Gold",
            1280,
            1500m,
            0m,
            true,
            new DateTimeOffset(2026, 8, 23, 15, 30, 0, TimeSpan.Zero));
        var snapshot = BuildSnapshot(
            tenantId,
            customers:
            [
                customer
            ],
            transactions:
            [
                new SaleRecord(
                    Guid.NewGuid(),
                    tenantId,
                    "INV-1001",
                    "Ayesha Malik",
                    3200m,
                    1200m,
                    "Completed",
                    new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero)),
                new SaleRecord(
                    Guid.NewGuid(),
                    tenantId,
                    "INV-1002",
                    "Ayesha Malik",
                    1800m,
                    700m,
                    "Completed",
                    new DateTimeOffset(2026, 8, 24, 11, 0, 0, TimeSpan.Zero))
            ]);

        var service = new WorkspaceQueryService(new StubWorkspaceRepository(snapshot));

        var hub = await service.GetCustomerHubAsync(tenantId, CancellationToken.None);

        Assert.Equal(1, hub.Metrics.TotalCustomers);
        Assert.Equal(1, hub.Metrics.LoyaltyMembers);
        Assert.Equal(5000m, hub.Metrics.LifetimeRevenue);
        Assert.Equal(5000m, hub.Customers.Single().LifetimeValue);
        Assert.Equal(2, hub.Customers.Single().VisitCount);
    }

    [Fact]
    public async Task GetOperationsHubAsync_ComputesComplianceAndCashMetrics()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = BuildSnapshot(
            tenantId,
            transactions:
            [
                new SaleRecord(
                    Guid.NewGuid(),
                    tenantId,
                    "INV-2001",
                    "Walk-in Customer",
                    2500m,
                    900m,
                    "Completed",
                    new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero),
                    PaymentMethod: "Cash",
                    FbrStatus: "QueuedOffline"),
                new SaleRecord(
                    Guid.NewGuid(),
                    tenantId,
                    "INV-2002",
                    "Walk-in Customer",
                    4300m,
                    1500m,
                    "Pending",
                    new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
                    PaymentMethod: "Card",
                    FbrStatus: "Reported",
                    FbrInvoiceNumber: "FBR-778"),
                new SaleRecord(
                    Guid.NewGuid(),
                    tenantId,
                    "INV-2003",
                    "Walk-in Customer",
                    1200m,
                    400m,
                    "Completed",
                    new DateTimeOffset(2026, 8, 24, 14, 0, 0, TimeSpan.Zero),
                    PaymentMethod: "Cash",
                    FbrStatus: "Rejected",
                    FbrErrorMessage: "Token expired")
            ],
            cashShifts:
            [
                new CashShift(
                    Guid.NewGuid(),
                    tenantId,
                    Guid.NewGuid(),
                    "Front Desk Cashier",
                    "Front Register 1",
                    new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero),
                    null,
                    10000m,
                    3700m,
                    0m,
                    0m,
                    13700m,
                    13750m,
                    "Open"),
                new CashShift(
                    Guid.NewGuid(),
                    tenantId,
                    Guid.NewGuid(),
                    "Store Manager",
                    "Counter Register 2",
                    new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 23, 17, 0, 0, TimeSpan.Zero),
                    8000m,
                    1000m,
                    0m,
                    0m,
                    9000m,
                    8800m,
                    "Needs Review")
            ]);

        var service = new WorkspaceQueryService(new StubWorkspaceRepository(snapshot));

        var hub = await service.GetOperationsHubAsync(tenantId, CancellationToken.None);

        Assert.Equal(1, hub.Cash.OpenRegisters);
        Assert.Equal(3700m, hub.Cash.TodayCashSales);
        Assert.Equal(1, hub.Compliance.QueuedFbrInvoices);
        Assert.Equal(1, hub.Compliance.ReportedInvoices);
        Assert.Equal(1, hub.Compliance.FailedInvoices);
        Assert.Equal(2, hub.Compliance.PendingApprovals);
        Assert.NotEmpty(hub.ModuleGroups);
    }

    [Fact]
    public async Task ImportInventoryAsync_ImportsCsvRowsIntoWorkspace()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var repository = new StubWorkspaceRepository(BuildSnapshot(tenantId, dailyFigures: Array.Empty<DailyBusinessFigure>()));
        var queryService = new WorkspaceQueryService(repository);
        var inventoryService = new InventoryManagementService(repository, queryService);
        var csv = Encoding.UTF8.GetBytes(
            "SKU,Name,Category,Unit Price,Warehouse,In Hand,Reserved,Reorder Level,Is Favorite,Is Quick Sale,Visual Code\n" +
            "SKU-1001,Desk Lamp,Lighting,4500,Main Warehouse,12,0,3,Yes,Yes,LAMP01\n" +
            "SKU-1002,Floor Mat,Home,900,,7,1,2,No,Yes,MAT02");

        var result = await inventoryService.ImportInventoryAsync(
            tenantId,
            new InventoryImportFileDto("inventory.csv", csv),
            CancellationToken.None);

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(2, result.Inventory.Items.Count);
        Assert.Contains(result.Inventory.Items, item => item.Sku == "SKU-1001" && item.InHand == 12);
        Assert.Contains(result.Inventory.Items, item => item.Sku == "SKU-1002" && item.Warehouse == "Main Warehouse");
    }

    private static WorkspaceSnapshot BuildSnapshot(
        Guid tenantId,
        IReadOnlyList<CustomerProfile>? customers = null,
        IReadOnlyList<SaleRecord>? transactions = null,
        IReadOnlyList<CashShift>? cashShifts = null,
        IReadOnlyList<DailyBusinessFigure>? dailyFigures = null)
    {
        return new WorkspaceSnapshot(
            new Tenant(tenantId, "demo-retail", "Demo Retail Company", "General Retail", "Business"),
            new Company(Guid.NewGuid(), tenantId, "Demo Retail Company", "PKR", "Asia/Karachi", "Pakistan"),
            new AppUser(Guid.NewGuid(), tenantId, Guid.NewGuid(), "admin@omnibusiness.local", "Admin", "Owner", "x:y"),
            new PosCustomer("Walk-in Customer", "Retail Pricing", "W"),
            Array.Empty<Branch>(),
            dailyFigures ??
            [
                new DailyBusinessFigure(new DateOnly(2026, 8, 22), 100m, 75m, 50m),
                new DailyBusinessFigure(new DateOnly(2026, 8, 23), 125m, 45m, 80m)
            ],
            Array.Empty<TrendPoint>(),
            Array.Empty<TopSellingItem>(),
            Array.Empty<BranchPerformance>(),
            Array.Empty<Product>(),
            transactions ?? Array.Empty<SaleRecord>(),
            Array.Empty<CartLine>(),
            new FormDefinition("form", "Product Custom Fields", "Desc", "field", Array.Empty<FormLibraryField>(), Array.Empty<FormCanvasField>()),
            Users: Array.Empty<AppUser>(),
            Customers: customers,
            StockAdjustments: Array.Empty<StockAdjustmentRecord>(),
            Vendors: Array.Empty<Vendor>(),
            PurchaseOrders: Array.Empty<PurchaseOrder>(),
            StockTransfers: Array.Empty<StockTransfer>(),
            CashShifts: cashShifts);
    }

    private sealed class StubWorkspaceRepository(WorkspaceSnapshot initialSnapshot) : IWorkspaceRepository
    {
        private WorkspaceSnapshot snapshot = initialSnapshot;

        public Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);

        public Task<WorkspaceSnapshot> UpdateWorkspaceSnapshotAsync(
            Func<WorkspaceSnapshot, WorkspaceSnapshot> update,
            CancellationToken cancellationToken)
        {
            snapshot = update(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<AppUser?> GetUserByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken) => Task.FromResult<AppUser?>(snapshot.AdminUser);

        public Task<AppUser?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<AppUser?>(snapshot.AdminUser);
    }
}
