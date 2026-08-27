using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Abstractions.Security;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Infrastructure.Persistence;

/// <summary>
/// Cloud persistence provider backing the same <see cref="IWorkspaceRepository"/> contract as
/// <see cref="LocalJsonWorkspaceRepository"/>, so the Application services, controllers and auth
/// are completely unaware of the store. Selected by <c>Persistence:Provider = Supabase|Postgres</c>.
///
/// Design notes:
///  * Registered as a singleton with an in-memory cached snapshot (valid because the cloud API
///    runs single-instance; see docs/go-live/architecture.md). The cache is authoritative once
///    loaded because every write goes through this instance.
///  * <see cref="UpdateWorkspaceSnapshotAsync"/> diffs the mutated snapshot against the cached one
///    (<see cref="WorkspaceSnapshotDiffer"/>) and writes only the changed rows inside one
///    transaction, so a checkout inserts one sale + its lines instead of rewriting history.
///  * On an empty database it self-seeds from the same <c>foundation.json</c> the LocalJson
///    provider uses, reusing <see cref="WorkspaceSnapshotNormalization"/> for byte-parity of shape.
/// </summary>
public sealed class PostgresWorkspaceRepository : IWorkspaceRepository
{
    static PostgresWorkspaceRepository()
    {
        // Map snake_case columns (tenant_id) to PascalCase record parameters (TenantId).
        // Global, but nothing else in the process uses Dapper (LocalJson uses System.Text.Json).
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private readonly IHostEnvironment _environment;
    private readonly PersistenceOptions _options;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly JsonSerializerOptions _seedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private WorkspaceSnapshot? _cachedSnapshot;

    public PostgresWorkspaceRepository(
        IHostEnvironment environment,
        IOptions<PersistenceOptions> options,
        IPasswordHasher passwordHasher)
    {
        _environment = environment;
        _options = options.Value;
        _passwordHasher = passwordHasher;

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Persistence:ConnectionString is required for the Supabase/Postgres provider. " +
                "Supply it via the Persistence__ConnectionString environment variable.");
        }
    }

    public async Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            return _cachedSnapshot ??= await LoadOrSeedAsync(cancellationToken);
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
            var current = _cachedSnapshot ??= await LoadOrSeedAsync(cancellationToken);
            var updated = WorkspaceSnapshotNormalization.Normalize(update(current));

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await PersistAsync(connection, transaction, current, updated, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _cachedSnapshot = updated;
            return updated;
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

    private NpgsqlConnection CreateConnection() => new(_options.ConnectionString);

    // ---------------------------------------------------------------------
    // Load / seed
    // ---------------------------------------------------------------------
    private async Task<WorkspaceSnapshot> LoadOrSeedAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var loaded = await LoadFromDatabaseAsync(connection, cancellationToken);
        if (loaded is not null)
        {
            return WorkspaceSnapshotNormalization.Normalize(loaded);
        }

        if (!_options.InitializeFromSeedOnFirstRun)
        {
            throw new InvalidOperationException(
                "The Postgres workspace is empty and automatic seeding is disabled " +
                "(Persistence:InitializeFromSeedOnFirstRun = false).");
        }

        var seeded = SeedBootstrapper.Apply(
            await ReadSeedSnapshotAsync(cancellationToken),
            _options,
            _passwordHasher);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PersistAsync(connection, transaction, previous: null, current: seeded, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return seeded;
    }

    private static async Task<WorkspaceSnapshot?> LoadFromDatabaseAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var tenant = await connection.QuerySingleOrDefaultAsync<Tenant>(
            new CommandDefinition("select * from tenants limit 1", cancellationToken: cancellationToken));
        if (tenant is null)
        {
            return null;
        }

        var tenantId = tenant.Id;
        var scope = new { T = tenantId };

        var company = await connection.QuerySingleOrDefaultAsync<Company>(
            Read("select * from companies where tenant_id = @T limit 1", scope, cancellationToken))
            ?? throw new InvalidOperationException("Workspace tenant row exists but its company row is missing.");

        var users = (await connection.QueryAsync<AppUser>(
            Read("select * from app_users where tenant_id = @T", scope, cancellationToken))).ToArray();
        if (users.Length == 0)
        {
            throw new InvalidOperationException("Workspace tenant row exists but has no users.");
        }

        var adminId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            Read("select id from app_users where tenant_id = @T and is_admin = true limit 1", scope, cancellationToken));
        var adminUser = users.FirstOrDefault(user => user.Id == adminId)
            ?? users.FirstOrDefault(user => string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            ?? users[0];

        var branches = (await connection.QueryAsync<Branch>(
            Read("select * from branches where tenant_id = @T order by is_primary desc, name", scope, cancellationToken))).ToArray();

        var products = (await connection.QueryAsync<Product>(
            Read("select * from products where tenant_id = @T order by name", scope, cancellationToken))).ToArray();

        var customers = (await connection.QueryAsync<CustomerProfile>(
            Read("select * from customers where tenant_id = @T", scope, cancellationToken))).ToArray();

        var stockAdjustments = (await connection.QueryAsync<StockAdjustmentRecord>(
            Read("select * from stock_adjustments where tenant_id = @T order by occurred_at desc", scope, cancellationToken))).ToArray();

        var vendors = (await connection.QueryAsync<Vendor>(
            Read("select * from vendors where tenant_id = @T order by name", scope, cancellationToken))).ToArray();

        var purchaseOrders = (await connection.QueryAsync<PurchaseOrder>(
            Read("select * from purchase_orders where tenant_id = @T order by created_at desc", scope, cancellationToken))).ToArray();

        var stockTransfers = (await connection.QueryAsync<StockTransfer>(
            Read("select * from stock_transfers where tenant_id = @T order by created_at desc", scope, cancellationToken))).ToArray();

        var cashShifts = (await connection.QueryAsync<CashShift>(
            Read("select * from cash_shifts where tenant_id = @T order by opened_at desc", scope, cancellationToken))).ToArray();

        // Sales: load headers, then lines, then compose (Dapper cannot map the Lines child directly).
        var saleHeaders = (await connection.QueryAsync<SaleRecord>(
            Read("select * from sales where tenant_id = @T order by occurred_at desc", scope, cancellationToken))).ToArray();
        var lineRows = await connection.QueryAsync<SaleLineRow>(
            Read(
                "select sl.* from sale_lines sl join sales s on s.id = sl.sale_id " +
                "where s.tenant_id = @T order by sl.sale_id, sl.ordinal",
                scope,
                cancellationToken));
        var linesBySale = lineRows
            .GroupBy(row => row.SaleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SaleLine>)group
                    .Select(row => new SaleLine(row.ProductId, row.Sku, row.Name, row.Quantity, row.UnitPrice, row.LineTotal))
                    .ToArray());
        var sales = saleHeaders
            .Select(sale => sale with
            {
                Lines = linesBySale.TryGetValue(sale.Id, out var lines) ? lines : Array.Empty<SaleLine>()
            })
            .ToArray();

        var activeCustomer = await connection.QuerySingleOrDefaultAsync<PosCustomer>(
            Read("select name, pricing_tier, avatar_letter from pos_active_customer where tenant_id = @T limit 1", scope, cancellationToken));

        var dailyFigures = (await connection.QueryAsync<DailyBusinessFigure>(
            Read("select date, sales, purchases, gross_profit from daily_figures where tenant_id = @T order by ordinal", scope, cancellationToken))).ToArray();
        var salesTrend = (await connection.QueryAsync<TrendPoint>(
            Read("select label, value from sales_trend where tenant_id = @T order by ordinal", scope, cancellationToken))).ToArray();
        var topSelling = (await connection.QueryAsync<TopSellingItem>(
            Read("select name, units, revenue from top_selling where tenant_id = @T order by ordinal", scope, cancellationToken))).ToArray();
        var branchPerformance = (await connection.QueryAsync<BranchPerformance>(
            Read("select branch_name, percentage from branch_performance where tenant_id = @T order by ordinal", scope, cancellationToken))).ToArray();
        var activeCart = (await connection.QueryAsync<CartLine>(
            Read("select product_id, name, quantity, unit_price, allow_quantity_edit from cart_lines where tenant_id = @T order by ordinal", scope, cancellationToken))).ToArray();

        var productCustomFields = await LoadFormDefinitionAsync(connection, tenantId, cancellationToken);
        var subscriptionSettings = await LoadSubscriptionSettingsAsync(connection, tenantId, cancellationToken);

        var nextSaleSequence = await connection.QuerySingleOrDefaultAsync<int?>(
            Read("select next_sale_sequence from workspace_counters where tenant_id = @T limit 1", scope, cancellationToken));

        return new WorkspaceSnapshot(
            tenant,
            company,
            adminUser,
            activeCustomer ?? new PosCustomer("Walk-in Customer", "Retail Pricing", "W"),
            branches,
            dailyFigures,
            salesTrend,
            topSelling,
            branchPerformance,
            products,
            sales,
            activeCart,
            productCustomFields,
            Users: users,
            Customers: customers,
            StockAdjustments: stockAdjustments,
            Vendors: vendors,
            PurchaseOrders: purchaseOrders,
            StockTransfers: stockTransfers,
            CashShifts: cashShifts,
            SubscriptionSettings: subscriptionSettings,
            NextSaleSequence: nextSaleSequence ?? 8902);
    }

    private static async Task<FormDefinition> LoadFormDefinitionAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var scope = new { T = tenantId };
        var form = await connection.QuerySingleOrDefaultAsync<FormDefinition>(
            Read("select id, title, description, selected_field_id from form_definitions where tenant_id = @T limit 1", scope, cancellationToken));

        if (form is null)
        {
            return new FormDefinition(
                "product-custom-fields",
                "Product Fields",
                string.Empty,
                string.Empty,
                Array.Empty<FormLibraryField>(),
                Array.Empty<FormCanvasField>());
        }

        var formScope = new { F = form.Id };
        var library = (await connection.QueryAsync<FormLibraryField>(
            Read("select key, label, \"group\", icon from form_library_fields where form_id = @F order by ordinal", formScope, cancellationToken))).ToArray();
        var canvas = (await connection.QueryAsync<FormCanvasField>(
            Read(
                "select field_id, label, type, required, placeholder, help_text, default_value, is_read_only, min_value, max_value " +
                "from form_canvas_fields where form_id = @F order by ordinal",
                formScope,
                cancellationToken))).ToArray();

        return form with { Library = library, Canvas = canvas };
    }

    private static async Task<SubscriptionPlanSettings?> LoadSubscriptionSettingsAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var scope = new { T = tenantId };
        var settings = await connection.QuerySingleOrDefaultAsync<SubscriptionPlanSettings>(
            Read(
                "select plan_code, plan_name, currency, base_monthly_price, included_users, included_branches, allow_custom_module_overrides " +
                "from subscription_plan_settings where tenant_id = @T limit 1",
                scope,
                cancellationToken));

        if (settings is null)
        {
            return null;
        }

        var entitlements = (await connection.QueryAsync<ModuleEntitlement>(
            Read("select module_key, enabled, add_on_monthly_price from module_entitlements where tenant_id = @T order by ordinal", scope, cancellationToken))).ToArray();

        return settings with { ModuleEntitlements = entitlements };
    }

    private async Task<WorkspaceSnapshot> ReadSeedSnapshotAsync(CancellationToken cancellationToken)
    {
        var seedPath = ResolveSeedPath(_options.SeedPath);
        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException($"Seed workspace file '{seedPath}' was not found.", seedPath);
        }

        await using var stream = new FileStream(seedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<WorkspaceSnapshot>(stream, _seedJsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to load seed workspace data from '{seedPath}'.");
    }

    private string ResolveSeedPath(string configuredPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configuredPath);

        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, expanded));
    }

    // ---------------------------------------------------------------------
    // Persist (diff-based write of the whole snapshot in one transaction)
    // ---------------------------------------------------------------------
    private static async Task PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkspaceSnapshot? previous,
        WorkspaceSnapshot current,
        CancellationToken cancellationToken)
    {
        var tenantId = current.Tenant.Id;

        // --- Singletons (cheap unconditional upserts) ---
        await Exec(connection, transaction, cancellationToken,
            """
            insert into tenants (id, slug, name, industry_template, subscription_plan)
            values (@Id, @Slug, @Name, @IndustryTemplate, @SubscriptionPlan)
            on conflict (id) do update set
                slug = excluded.slug, name = excluded.name,
                industry_template = excluded.industry_template, subscription_plan = excluded.subscription_plan;
            """, current.Tenant);

        await Exec(connection, transaction, cancellationToken,
            """
            insert into companies (id, tenant_id, name, base_currency, time_zone, country)
            values (@Id, @TenantId, @Name, @BaseCurrency, @TimeZone, @Country)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, name = excluded.name, base_currency = excluded.base_currency,
                time_zone = excluded.time_zone, country = excluded.country;
            """, current.Company);

        await Exec(connection, transaction, cancellationToken,
            """
            insert into pos_active_customer (tenant_id, name, pricing_tier, avatar_letter)
            values (@TenantId, @Name, @PricingTier, @AvatarLetter)
            on conflict (tenant_id) do update set
                name = excluded.name, pricing_tier = excluded.pricing_tier, avatar_letter = excluded.avatar_letter;
            """,
            new { TenantId = tenantId, current.ActiveCustomer.Name, current.ActiveCustomer.PricingTier, current.ActiveCustomer.AvatarLetter });

        await Exec(connection, transaction, cancellationToken,
            """
            insert into workspace_counters (tenant_id, next_sale_sequence)
            values (@TenantId, @NextSaleSequence)
            on conflict (tenant_id) do update set next_sale_sequence = excluded.next_sale_sequence;
            """,
            new { TenantId = tenantId, current.NextSaleSequence });

        await PersistSubscriptionAsync(connection, transaction, tenantId, current.SubscriptionSettings, cancellationToken);
        await PersistFormDefinitionAsync(connection, transaction, tenantId, current.ProductCustomFields, cancellationToken);

        // --- Small bounded collections (replace-all) ---
        await ReplaceAllAsync(connection, transaction, cancellationToken, tenantId,
            "delete from daily_figures where tenant_id = @TenantId",
            "insert into daily_figures (tenant_id, ordinal, date, sales, purchases, gross_profit) values (@TenantId, @Ordinal, @Date, @Sales, @Purchases, @GrossProfit)",
            current.DailyFigures,
            (i, f) => new { TenantId = tenantId, Ordinal = i, f.Date, f.Sales, f.Purchases, f.GrossProfit });

        await ReplaceAllAsync(connection, transaction, cancellationToken, tenantId,
            "delete from sales_trend where tenant_id = @TenantId",
            "insert into sales_trend (tenant_id, ordinal, label, value) values (@TenantId, @Ordinal, @Label, @Value)",
            current.SalesTrend,
            (i, p) => new { TenantId = tenantId, Ordinal = i, p.Label, p.Value });

        await ReplaceAllAsync(connection, transaction, cancellationToken, tenantId,
            "delete from top_selling where tenant_id = @TenantId",
            "insert into top_selling (tenant_id, ordinal, name, units, revenue) values (@TenantId, @Ordinal, @Name, @Units, @Revenue)",
            current.TopSelling,
            (i, t) => new { TenantId = tenantId, Ordinal = i, t.Name, t.Units, t.Revenue });

        await ReplaceAllAsync(connection, transaction, cancellationToken, tenantId,
            "delete from branch_performance where tenant_id = @TenantId",
            "insert into branch_performance (tenant_id, ordinal, branch_name, percentage) values (@TenantId, @Ordinal, @BranchName, @Percentage)",
            current.BranchPerformance,
            (i, b) => new { TenantId = tenantId, Ordinal = i, b.BranchName, b.Percentage });

        await ReplaceAllAsync(connection, transaction, cancellationToken, tenantId,
            "delete from cart_lines where tenant_id = @TenantId",
            "insert into cart_lines (tenant_id, ordinal, product_id, name, quantity, unit_price, allow_quantity_edit) values (@TenantId, @Ordinal, @ProductId, @Name, @Quantity, @UnitPrice, @AllowQuantityEdit)",
            current.ActiveCart,
            (i, c) => new { TenantId = tenantId, Ordinal = i, c.ProductId, c.Name, c.Quantity, c.UnitPrice, c.AllowQuantityEdit });

        // --- Unbounded business tables (diff-by-id) ---
        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.Branches, current.Branches, b => b.Id),
            """
            insert into branches (id, tenant_id, code, name, warehouse_name, is_primary)
            values (@Id, @TenantId, @Code, @Name, @WarehouseName, @IsPrimary)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, code = excluded.code, name = excluded.name,
                warehouse_name = excluded.warehouse_name, is_primary = excluded.is_primary;
            """,
            "delete from branches where id in @Ids");

        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.Products, current.Products, p => p.Id),
            """
            insert into products (id, tenant_id, sku, name, category, unit_price, in_hand, reserved, warehouse, status,
                is_favorite, is_quick_sale, is_low_stock, visual_code, reorder_level, is_archived)
            values (@Id, @TenantId, @Sku, @Name, @Category, @UnitPrice, @InHand, @Reserved, @Warehouse, @Status,
                @IsFavorite, @IsQuickSale, @IsLowStock, @VisualCode, @ReorderLevel, @IsArchived)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, sku = excluded.sku, name = excluded.name, category = excluded.category,
                unit_price = excluded.unit_price, in_hand = excluded.in_hand, reserved = excluded.reserved,
                warehouse = excluded.warehouse, status = excluded.status, is_favorite = excluded.is_favorite,
                is_quick_sale = excluded.is_quick_sale, is_low_stock = excluded.is_low_stock,
                visual_code = excluded.visual_code, reorder_level = excluded.reorder_level, is_archived = excluded.is_archived;
            """,
            "delete from products where id in @Ids");

        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.Customers, current.Customers, c => c.Id),
            """
            insert into customers (id, tenant_id, name, pricing_tier, avatar_letter, phone_number, is_walk_in, email,
                loyalty_tier, loyalty_points, store_credit_balance, gift_card_balance, marketing_opt_in, last_visit_at)
            values (@Id, @TenantId, @Name, @PricingTier, @AvatarLetter, @PhoneNumber, @IsWalkIn, @Email,
                @LoyaltyTier, @LoyaltyPoints, @StoreCreditBalance, @GiftCardBalance, @MarketingOptIn, @LastVisitAt)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, name = excluded.name, pricing_tier = excluded.pricing_tier,
                avatar_letter = excluded.avatar_letter, phone_number = excluded.phone_number, is_walk_in = excluded.is_walk_in,
                email = excluded.email, loyalty_tier = excluded.loyalty_tier, loyalty_points = excluded.loyalty_points,
                store_credit_balance = excluded.store_credit_balance, gift_card_balance = excluded.gift_card_balance,
                marketing_opt_in = excluded.marketing_opt_in, last_visit_at = excluded.last_visit_at;
            """,
            "delete from customers where id in @Ids");

        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.StockAdjustments, current.StockAdjustments, a => a.Id),
            """
            insert into stock_adjustments (id, tenant_id, product_id, product_name, quantity_delta, reason, performed_by, occurred_at)
            values (@Id, @TenantId, @ProductId, @ProductName, @QuantityDelta, @Reason, @PerformedBy, @OccurredAt)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, product_id = excluded.product_id, product_name = excluded.product_name,
                quantity_delta = excluded.quantity_delta, reason = excluded.reason, performed_by = excluded.performed_by,
                occurred_at = excluded.occurred_at;
            """,
            "delete from stock_adjustments where id in @Ids");

        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.Vendors, current.Vendors, v => v.Id),
            """
            insert into vendors (id, tenant_id, name, contact_person, phone_number, city, lead_time_label, payment_terms, status)
            values (@Id, @TenantId, @Name, @ContactPerson, @PhoneNumber, @City, @LeadTimeLabel, @PaymentTerms, @Status)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, name = excluded.name, contact_person = excluded.contact_person,
                phone_number = excluded.phone_number, city = excluded.city, lead_time_label = excluded.lead_time_label,
                payment_terms = excluded.payment_terms, status = excluded.status;
            """,
            "delete from vendors where id in @Ids");

        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.PurchaseOrders, current.PurchaseOrders, o => o.Id),
            """
            insert into purchase_orders (id, tenant_id, vendor_id, purchase_order_no, vendor_name, status, created_at,
                expected_at, total_amount, line_count, ordered_units, received_units)
            values (@Id, @TenantId, @VendorId, @PurchaseOrderNo, @VendorName, @Status, @CreatedAt,
                @ExpectedAt, @TotalAmount, @LineCount, @OrderedUnits, @ReceivedUnits)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, vendor_id = excluded.vendor_id, purchase_order_no = excluded.purchase_order_no,
                vendor_name = excluded.vendor_name, status = excluded.status, created_at = excluded.created_at,
                expected_at = excluded.expected_at, total_amount = excluded.total_amount, line_count = excluded.line_count,
                ordered_units = excluded.ordered_units, received_units = excluded.received_units;
            """,
            "delete from purchase_orders where id in @Ids");

        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.StockTransfers, current.StockTransfers, t => t.Id),
            """
            insert into stock_transfers (id, tenant_id, transfer_no, from_branch_name, to_branch_name, status, created_at,
                expected_at, units, requested_by)
            values (@Id, @TenantId, @TransferNo, @FromBranchName, @ToBranchName, @Status, @CreatedAt,
                @ExpectedAt, @Units, @RequestedBy)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, transfer_no = excluded.transfer_no, from_branch_name = excluded.from_branch_name,
                to_branch_name = excluded.to_branch_name, status = excluded.status, created_at = excluded.created_at,
                expected_at = excluded.expected_at, units = excluded.units, requested_by = excluded.requested_by;
            """,
            "delete from stock_transfers where id in @Ids");

        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous?.CashShifts, current.CashShifts, s => s.Id),
            """
            insert into cash_shifts (id, tenant_id, user_id, cashier_name, register_name, opened_at, closed_at,
                opening_float, cash_sales, refunds, paid_outs, expected_cash, counted_cash, status)
            values (@Id, @TenantId, @UserId, @CashierName, @RegisterName, @OpenedAt, @ClosedAt,
                @OpeningFloat, @CashSales, @Refunds, @PaidOuts, @ExpectedCash, @CountedCash, @Status)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, user_id = excluded.user_id, cashier_name = excluded.cashier_name,
                register_name = excluded.register_name, opened_at = excluded.opened_at, closed_at = excluded.closed_at,
                opening_float = excluded.opening_float, cash_sales = excluded.cash_sales, refunds = excluded.refunds,
                paid_outs = excluded.paid_outs, expected_cash = excluded.expected_cash, counted_cash = excluded.counted_cash,
                status = excluded.status;
            """,
            "delete from cash_shifts where id in @Ids");

        await PersistUsersAsync(connection, transaction, tenantId, previous?.Users, current.Users, current.AdminUser.Id, cancellationToken);
        await PersistSalesAsync(connection, transaction, previous?.RecentTransactions, current.RecentTransactions, cancellationToken);
    }

    private static async Task PersistUsersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        IReadOnlyList<AppUser>? previous,
        IReadOnlyList<AppUser>? current,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        await ApplyDeltaAsync(connection, transaction, cancellationToken,
            WorkspaceSnapshotDiffer.DiffById(previous, current, u => u.Id),
            """
            insert into app_users (id, tenant_id, branch_id, email, display_name, role, password_hash)
            values (@Id, @TenantId, @BranchId, @Email, @DisplayName, @Role, @PasswordHash)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, branch_id = excluded.branch_id, email = excluded.email,
                display_name = excluded.display_name, role = excluded.role, password_hash = excluded.password_hash;
            """,
            "delete from app_users where id in @Ids");

        // is_admin is not part of the AppUser record; reconcile it in one statement so a change of
        // admin designation is honored even when no other user field changed.
        await Exec(connection, transaction, cancellationToken,
            "update app_users set is_admin = (id = @AdminId) where tenant_id = @TenantId",
            new { AdminId = adminUserId, TenantId = tenantId });
    }

    private static async Task PersistSalesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<SaleRecord>? previous,
        IReadOnlyList<SaleRecord>? current,
        CancellationToken cancellationToken)
    {
        var delta = WorkspaceSnapshotDiffer.DiffById(previous, current, s => s.Id, WorkspaceSnapshotDiffer.SalesEqual);

        const string upsertHeader =
            """
            insert into sales (id, tenant_id, reference_no, customer_name, amount, gross_profit, status, occurred_at,
                item_count, discount, tax, payment_method, cashier_name, received_amount, change_amount,
                fbr_status, fbr_invoice_number, fbr_error_message, fbr_reported_at)
            values (@Id, @TenantId, @ReferenceNo, @CustomerName, @Amount, @GrossProfit, @Status, @OccurredAt,
                @ItemCount, @Discount, @Tax, @PaymentMethod, @CashierName, @ReceivedAmount, @ChangeAmount,
                @FbrStatus, @FbrInvoiceNumber, @FbrErrorMessage, @FbrReportedAt)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, reference_no = excluded.reference_no, customer_name = excluded.customer_name,
                amount = excluded.amount, gross_profit = excluded.gross_profit, status = excluded.status,
                occurred_at = excluded.occurred_at, item_count = excluded.item_count, discount = excluded.discount,
                tax = excluded.tax, payment_method = excluded.payment_method, cashier_name = excluded.cashier_name,
                received_amount = excluded.received_amount, change_amount = excluded.change_amount,
                fbr_status = excluded.fbr_status, fbr_invoice_number = excluded.fbr_invoice_number,
                fbr_error_message = excluded.fbr_error_message, fbr_reported_at = excluded.fbr_reported_at;
            """;
        const string insertLine =
            "insert into sale_lines (sale_id, ordinal, product_id, sku, name, quantity, unit_price, line_total) " +
            "values (@SaleId, @Ordinal, @ProductId, @Sku, @Name, @Quantity, @UnitPrice, @LineTotal)";

        foreach (var sale in delta.Upserts)
        {
            await Exec(connection, transaction, cancellationToken, upsertHeader, sale);

            // Replace this sale's lines (small child set); cheaper and simpler than diffing lines.
            await Exec(connection, transaction, cancellationToken,
                "delete from sale_lines where sale_id = @SaleId", new { SaleId = sale.Id });

            var lines = sale.Lines ?? Array.Empty<SaleLine>();
            if (lines.Count > 0)
            {
                var lineParams = new List<object>(lines.Count);
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    lineParams.Add(new
                    {
                        SaleId = sale.Id,
                        Ordinal = i,
                        line.ProductId,
                        line.Sku,
                        line.Name,
                        line.Quantity,
                        line.UnitPrice,
                        line.LineTotal
                    });
                }

                await Exec(connection, transaction, cancellationToken, insertLine, lineParams);
            }
        }

        if (delta.DeletedIds.Count > 0)
        {
            // sale_lines cascade via FK.
            await Exec(connection, transaction, cancellationToken,
                "delete from sales where id in @Ids", new { Ids = delta.DeletedIds.ToArray() });
        }
    }

    private static async Task PersistSubscriptionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        SubscriptionPlanSettings? settings,
        CancellationToken cancellationToken)
    {
        if (settings is null)
        {
            return;
        }

        await Exec(connection, transaction, cancellationToken,
            """
            insert into subscription_plan_settings (tenant_id, plan_code, plan_name, currency, base_monthly_price,
                included_users, included_branches, allow_custom_module_overrides)
            values (@TenantId, @PlanCode, @PlanName, @Currency, @BaseMonthlyPrice,
                @IncludedUsers, @IncludedBranches, @AllowCustomModuleOverrides)
            on conflict (tenant_id) do update set
                plan_code = excluded.plan_code, plan_name = excluded.plan_name, currency = excluded.currency,
                base_monthly_price = excluded.base_monthly_price, included_users = excluded.included_users,
                included_branches = excluded.included_branches, allow_custom_module_overrides = excluded.allow_custom_module_overrides;
            """,
            new
            {
                TenantId = tenantId,
                settings.PlanCode,
                settings.PlanName,
                settings.Currency,
                settings.BaseMonthlyPrice,
                settings.IncludedUsers,
                settings.IncludedBranches,
                settings.AllowCustomModuleOverrides
            });

        await Exec(connection, transaction, cancellationToken,
            "delete from module_entitlements where tenant_id = @TenantId", new { TenantId = tenantId });

        var entitlements = settings.ModuleEntitlements ?? Array.Empty<ModuleEntitlement>();
        if (entitlements.Count > 0)
        {
            var rows = new List<object>(entitlements.Count);
            for (var i = 0; i < entitlements.Count; i++)
            {
                var entitlement = entitlements[i];
                rows.Add(new
                {
                    TenantId = tenantId,
                    Ordinal = i,
                    entitlement.ModuleKey,
                    entitlement.Enabled,
                    entitlement.AddOnMonthlyPrice
                });
            }

            await Exec(connection, transaction, cancellationToken,
                "insert into module_entitlements (tenant_id, ordinal, module_key, enabled, add_on_monthly_price) " +
                "values (@TenantId, @Ordinal, @ModuleKey, @Enabled, @AddOnMonthlyPrice)",
                rows);
        }
    }

    private static async Task PersistFormDefinitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        FormDefinition form,
        CancellationToken cancellationToken)
    {
        // Drop any stale form definition for this tenant (id changes cascade-clean children).
        await Exec(connection, transaction, cancellationToken,
            "delete from form_definitions where tenant_id = @TenantId and id <> @Id",
            new { TenantId = tenantId, form.Id });

        await Exec(connection, transaction, cancellationToken,
            """
            insert into form_definitions (id, tenant_id, title, description, selected_field_id)
            values (@Id, @TenantId, @Title, @Description, @SelectedFieldId)
            on conflict (id) do update set
                tenant_id = excluded.tenant_id, title = excluded.title, description = excluded.description,
                selected_field_id = excluded.selected_field_id;
            """,
            new { form.Id, TenantId = tenantId, form.Title, form.Description, form.SelectedFieldId });

        await Exec(connection, transaction, cancellationToken,
            "delete from form_library_fields where form_id = @FormId", new { FormId = form.Id });

        var library = form.Library ?? Array.Empty<FormLibraryField>();
        if (library.Count > 0)
        {
            var rows = new List<object>(library.Count);
            for (var i = 0; i < library.Count; i++)
            {
                var field = library[i];
                rows.Add(new { FormId = form.Id, Ordinal = i, field.Key, field.Label, field.Group, field.Icon });
            }

            await Exec(connection, transaction, cancellationToken,
                "insert into form_library_fields (form_id, ordinal, key, label, \"group\", icon) " +
                "values (@FormId, @Ordinal, @Key, @Label, @Group, @Icon)",
                rows);
        }

        await Exec(connection, transaction, cancellationToken,
            "delete from form_canvas_fields where form_id = @FormId", new { FormId = form.Id });

        var canvas = form.Canvas ?? Array.Empty<FormCanvasField>();
        if (canvas.Count > 0)
        {
            var rows = new List<object>(canvas.Count);
            for (var i = 0; i < canvas.Count; i++)
            {
                var field = canvas[i];
                rows.Add(new
                {
                    FormId = form.Id,
                    Ordinal = i,
                    field.FieldId,
                    field.Label,
                    Type = field.Type.ToString(),
                    field.Required,
                    field.Placeholder,
                    field.HelpText,
                    field.DefaultValue,
                    field.IsReadOnly,
                    field.MinValue,
                    field.MaxValue
                });
            }

            await Exec(connection, transaction, cancellationToken,
                "insert into form_canvas_fields (form_id, ordinal, field_id, label, type, required, placeholder, " +
                "help_text, default_value, is_read_only, min_value, max_value) " +
                "values (@FormId, @Ordinal, @FieldId, @Label, @Type, @Required, @Placeholder, " +
                "@HelpText, @DefaultValue, @IsReadOnly, @MinValue, @MaxValue)",
                rows);
        }
    }

    // ---------------------------------------------------------------------
    // Small helpers
    // ---------------------------------------------------------------------
    private static async Task ReplaceAllAsync<T>(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken,
        Guid tenantId,
        string deleteSql,
        string insertSql,
        IReadOnlyList<T> items,
        Func<int, T, object> toParam)
    {
        await Exec(connection, transaction, cancellationToken, deleteSql, new { TenantId = tenantId });

        if (items.Count == 0)
        {
            return;
        }

        var rows = new List<object>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            rows.Add(toParam(i, items[i]));
        }

        await Exec(connection, transaction, cancellationToken, insertSql, rows);
    }

    private static async Task ApplyDeltaAsync<T>(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken,
        CollectionDelta<T> delta,
        string upsertSql,
        string deleteSql)
    {
        if (delta.Upserts.Count > 0)
        {
            await Exec(connection, transaction, cancellationToken, upsertSql, delta.Upserts);
        }

        if (delta.DeletedIds.Count > 0)
        {
            await Exec(connection, transaction, cancellationToken, deleteSql, new { Ids = delta.DeletedIds.ToArray() });
        }
    }

    private static Task<int> Exec(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken,
        string sql,
        object? param)
        => connection.ExecuteAsync(new CommandDefinition(sql, param, transaction, cancellationToken: cancellationToken));

    private static CommandDefinition Read(string sql, object? param, CancellationToken cancellationToken)
        => new(sql, param, cancellationToken: cancellationToken);

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

    private sealed record SaleLineRow(
        Guid SaleId,
        Guid ProductId,
        string Sku,
        string Name,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);
}
