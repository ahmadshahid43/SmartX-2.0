-- SmartX — Phase 1 Supabase Go-Live
-- 0001_init.sql : relational schema mapping OmniBusiness.Domain.Foundation.WorkspaceSnapshot.
--
-- Type mapping used throughout:
--   Guid            -> uuid
--   string          -> text
--   money (decimal) -> numeric(18,2)
--   int             -> integer
--   bool            -> boolean
--   DateOnly        -> date
--   DateTimeOffset  -> timestamptz
--   nullable / defaulted domain fields -> nullable columns
--
-- Every business table carries tenant_id (multi-tenant ready even though phase 1 ships a
-- single tenant per deployment). Child collections (sale_lines, form_* , module_entitlements,
-- and the analytics/cart tables that have no natural id) use an `ordinal` column to preserve
-- list order, and cascade from their parent.
--
-- The API connects as a dedicated least-privilege role and is the only writer; RLS is enabled
-- separately in 0002_rls.sql purely as defense-in-depth.

begin;

-- ---------------------------------------------------------------------------
-- Tenancy / identity
-- ---------------------------------------------------------------------------
create table if not exists tenants (
    id                uuid primary key,
    slug              text not null,
    name              text not null,
    industry_template text not null,
    subscription_plan text not null
);

create table if not exists companies (
    id            uuid primary key,
    tenant_id     uuid not null,
    name          text not null,
    base_currency text not null,
    time_zone     text not null,
    country       text not null
);

create table if not exists branches (
    id             uuid primary key,
    tenant_id      uuid not null,
    code           text not null,
    name           text not null,
    warehouse_name text not null,
    is_primary     boolean not null
);

create table if not exists app_users (
    id            uuid primary key,
    tenant_id     uuid not null,
    branch_id     uuid not null,
    email         text not null,
    display_name  text not null,
    role          text not null,
    password_hash text not null,
    -- Marks the WorkspaceSnapshot.AdminUser (not part of the AppUser record itself).
    is_admin      boolean not null default false
);

-- WorkspaceSnapshot.ActiveCustomer (PosCustomer): exactly one row per tenant.
create table if not exists pos_active_customer (
    tenant_id     uuid primary key,
    name          text not null,
    pricing_tier  text not null,
    avatar_letter text not null
);

-- ---------------------------------------------------------------------------
-- Catalog / commerce (diff-by-id, unbounded growth)
-- ---------------------------------------------------------------------------
create table if not exists products (
    id            uuid primary key,
    tenant_id     uuid not null,
    sku           text not null,
    name          text not null,
    category      text not null,
    unit_price    numeric(18,2) not null,
    in_hand       integer not null,
    reserved      integer not null,
    warehouse     text not null,
    status        text not null,
    is_favorite   boolean not null,
    is_quick_sale boolean not null,
    is_low_stock  boolean not null,
    visual_code   text not null,
    reorder_level integer not null default 5,
    is_archived   boolean not null default false
);

create table if not exists customers (
    id                    uuid primary key,
    tenant_id             uuid not null,
    name                  text not null,
    pricing_tier          text not null,
    avatar_letter         text not null,
    phone_number          text,
    is_walk_in            boolean not null default false,
    email                 text,
    loyalty_tier          text not null default 'Standard',
    loyalty_points        integer not null default 0,
    store_credit_balance  numeric(18,2) not null default 0,
    gift_card_balance     numeric(18,2) not null default 0,
    marketing_opt_in      boolean not null default false,
    last_visit_at         timestamptz
);

create table if not exists sales (
    id                  uuid primary key,
    tenant_id           uuid not null,
    reference_no        text not null,
    customer_name       text not null,
    amount              numeric(18,2) not null,
    gross_profit        numeric(18,2) not null,
    status              text not null,
    occurred_at         timestamptz not null,
    item_count          integer not null default 0,
    discount            numeric(18,2) not null default 0,
    tax                 numeric(18,2) not null default 0,
    payment_method      text not null default 'Cash',
    cashier_name        text not null default '',
    received_amount     numeric(18,2) not null default 0,
    change_amount       numeric(18,2) not null default 0,
    fbr_status          text not null default 'QueuedOffline',
    fbr_invoice_number  text,
    fbr_error_message   text,
    fbr_reported_at     timestamptz
);

create table if not exists sale_lines (
    sale_id     uuid not null references sales(id) on delete cascade,
    ordinal     integer not null,
    product_id  uuid not null,
    sku         text not null,
    name        text not null,
    quantity    integer not null,
    unit_price  numeric(18,2) not null,
    line_total  numeric(18,2) not null,
    primary key (sale_id, ordinal)
);

create table if not exists stock_adjustments (
    id             uuid primary key,
    tenant_id      uuid not null,
    product_id     uuid not null,
    product_name   text not null,
    quantity_delta integer not null,
    reason         text not null,
    performed_by   text not null,
    occurred_at    timestamptz not null
);

create table if not exists vendors (
    id             uuid primary key,
    tenant_id      uuid not null,
    name           text not null,
    contact_person text not null,
    phone_number   text not null,
    city           text not null,
    lead_time_label text not null,
    payment_terms  text not null,
    status         text not null default 'Active'
);

create table if not exists purchase_orders (
    id                uuid primary key,
    tenant_id         uuid not null,
    vendor_id         uuid not null,
    purchase_order_no text not null,
    vendor_name       text not null,
    status            text not null,
    created_at        timestamptz not null,
    expected_at       timestamptz,
    total_amount      numeric(18,2) not null,
    line_count        integer not null,
    ordered_units     integer not null,
    received_units    integer not null
);

create table if not exists stock_transfers (
    id               uuid primary key,
    tenant_id        uuid not null,
    transfer_no      text not null,
    from_branch_name text not null,
    to_branch_name   text not null,
    status           text not null,
    created_at       timestamptz not null,
    expected_at      timestamptz,
    units            integer not null,
    requested_by     text not null
);

create table if not exists cash_shifts (
    id            uuid primary key,
    tenant_id     uuid not null,
    user_id       uuid not null,
    cashier_name  text not null,
    register_name text not null,
    opened_at     timestamptz not null,
    closed_at     timestamptz,
    opening_float numeric(18,2) not null,
    cash_sales    numeric(18,2) not null,
    refunds       numeric(18,2) not null,
    paid_outs     numeric(18,2) not null,
    expected_cash numeric(18,2) not null,
    counted_cash  numeric(18,2) not null,
    status        text not null
);

-- ---------------------------------------------------------------------------
-- Small / bounded collections (replace-all on save; ordinal preserves order)
-- ---------------------------------------------------------------------------
create table if not exists cart_lines (
    tenant_id           uuid not null,
    ordinal             integer not null,
    product_id          uuid not null,
    name                text not null,
    quantity            integer not null,
    unit_price          numeric(18,2) not null,
    allow_quantity_edit boolean not null,
    primary key (tenant_id, ordinal)
);

create table if not exists daily_figures (
    tenant_id    uuid not null,
    ordinal      integer not null,
    date         date not null,
    sales        numeric(18,2) not null,
    purchases    numeric(18,2) not null,
    gross_profit numeric(18,2) not null,
    primary key (tenant_id, ordinal)
);

create table if not exists sales_trend (
    tenant_id uuid not null,
    ordinal   integer not null,
    label     text not null,
    value     numeric(18,2) not null,
    primary key (tenant_id, ordinal)
);

create table if not exists top_selling (
    tenant_id uuid not null,
    ordinal   integer not null,
    name      text not null,
    units     integer not null,
    revenue   numeric(18,2) not null,
    primary key (tenant_id, ordinal)
);

create table if not exists branch_performance (
    tenant_id   uuid not null,
    ordinal     integer not null,
    branch_name text not null,
    percentage  integer not null,
    primary key (tenant_id, ordinal)
);

-- ---------------------------------------------------------------------------
-- Customization: ProductCustomFields (FormDefinition + children)
-- ---------------------------------------------------------------------------
create table if not exists form_definitions (
    id                text primary key,
    tenant_id         uuid not null,
    title             text not null,
    description       text not null,
    selected_field_id text not null
);

create table if not exists form_library_fields (
    form_id  text not null references form_definitions(id) on delete cascade,
    ordinal  integer not null,
    key      text not null,
    label    text not null,
    "group"  text not null,
    icon     text not null,
    primary key (form_id, ordinal)
);

create table if not exists form_canvas_fields (
    form_id       text not null references form_definitions(id) on delete cascade,
    ordinal       integer not null,
    field_id      text not null,
    label         text not null,
    type          text not null,          -- FormFieldType enum name
    required      boolean not null,
    placeholder   text not null,
    help_text     text,
    default_value text,
    is_read_only  boolean not null,
    min_value     integer,
    max_value     integer,
    primary key (form_id, ordinal)
);

-- ---------------------------------------------------------------------------
-- Subscription / plan (SubscriptionPlanSettings + entitlements)
-- ---------------------------------------------------------------------------
create table if not exists subscription_plan_settings (
    tenant_id                     uuid primary key,
    plan_code                     text not null,
    plan_name                     text not null,
    currency                      text not null,
    base_monthly_price            numeric(18,2) not null,
    included_users                integer not null,
    included_branches             integer not null,
    allow_custom_module_overrides boolean not null
);

create table if not exists module_entitlements (
    tenant_id           uuid not null references subscription_plan_settings(tenant_id) on delete cascade,
    ordinal             integer not null,
    module_key          text not null,
    enabled             boolean not null,
    add_on_monthly_price numeric(18,2) not null,
    primary key (tenant_id, ordinal)
);

-- ---------------------------------------------------------------------------
-- Workspace counters (WorkspaceSnapshot.NextSaleSequence)
-- ---------------------------------------------------------------------------
create table if not exists workspace_counters (
    tenant_id          uuid primary key,
    next_sale_sequence integer not null default 8902
);

-- ---------------------------------------------------------------------------
-- Indexes to support tenant-scoped reads and the ordered sales history load.
-- ---------------------------------------------------------------------------
create index if not exists ix_products_tenant           on products (tenant_id);
create index if not exists ix_customers_tenant          on customers (tenant_id);
create index if not exists ix_sales_tenant_occurred     on sales (tenant_id, occurred_at desc);
create index if not exists ix_sale_lines_sale           on sale_lines (sale_id);
create index if not exists ix_stock_adjustments_tenant  on stock_adjustments (tenant_id, occurred_at desc);
create index if not exists ix_vendors_tenant            on vendors (tenant_id);
create index if not exists ix_purchase_orders_tenant    on purchase_orders (tenant_id, created_at desc);
create index if not exists ix_stock_transfers_tenant    on stock_transfers (tenant_id, created_at desc);
create index if not exists ix_cash_shifts_tenant        on cash_shifts (tenant_id, opened_at desc);
create index if not exists ix_app_users_tenant          on app_users (tenant_id);
create index if not exists ix_branches_tenant           on branches (tenant_id);

commit;
