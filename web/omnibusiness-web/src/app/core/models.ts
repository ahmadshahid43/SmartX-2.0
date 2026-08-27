export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: WorkspaceUser;
}

export interface WorkspaceUser {
  userId: string;
  tenantId: string;
  branchId: string;
  email: string;
  displayName: string;
  role: string;
}

export interface WorkspaceContext {
  tenant: TenantSummary;
  company: CompanySummary;
  user: WorkspaceUser;
  branches: BranchSummary[];
  access: WorkspaceModuleAccess;
}

export interface WorkspaceModuleAccess {
  planCode: string;
  planName: string;
  currency: string;
  baseMonthlyPrice: number;
  includedUsers: number;
  includedBranches: number;
  allowCustomModuleOverrides: boolean;
  enabledModules: string[];
}

export interface ModuleSettings {
  access: WorkspaceModuleAccess;
  estimatedMonthlyTotal: number;
  groups: ModuleSettingsGroup[];
}

export interface ModuleSettingsGroup {
  key: string;
  title: string;
  description: string;
  modules: WorkspaceModule[];
}

export interface WorkspaceModule {
  moduleKey: string;
  title: string;
  description: string;
  category: string;
  icon: string;
  route: string | null;
  deliveryStatus: string;
  recommendedPlan: string;
  isEnabled: boolean;
  hasScreen: boolean;
  addOnMonthlyPrice: number;
  capabilities: string[];
}

export interface SaveModuleSettingsRequest {
  planCode: string;
  planName: string;
  currency: string;
  baseMonthlyPrice: number;
  includedUsers: number;
  includedBranches: number;
  allowCustomModuleOverrides: boolean;
  modules: SaveModuleEntitlementRequest[];
}

export interface SaveModuleEntitlementRequest {
  moduleKey: string;
  enabled: boolean;
  addOnMonthlyPrice: number;
}

export interface WorkspaceUsers {
  items: WorkspaceStaff[];
}

export interface CustomerHub {
  metrics: CustomerMetrics;
  customers: CustomerProfile[];
}

export interface CustomerMetrics {
  totalCustomers: number;
  loyaltyMembers: number;
  lifetimeRevenue: number;
  activeInLast30Days: number;
}

export interface CustomerProfile {
  customerId: string;
  name: string;
  pricingTier: string;
  avatarLetter: string;
  loyaltyTier: string;
  loyaltyPoints: number;
  storeCreditBalance: number;
  giftCardBalance: number;
  phoneNumber: string | null;
  email: string | null;
  marketingOptIn: boolean;
  isWalkIn: boolean;
  lastVisitAt: string | null;
  lifetimeValue: number;
  visitCount: number;
}

export interface ProcurementHub {
  metrics: ProcurementMetrics;
  vendors: VendorSummary[];
  purchaseOrders: PurchaseOrderSummary[];
  stockTransfers: StockTransferSummary[];
}

export interface ProcurementMetrics {
  activeVendors: number;
  openPurchaseOrders: number;
  purchasePipelineAmount: number;
  inTransitUnits: number;
}

export interface VendorSummary {
  vendorId: string;
  name: string;
  contactPerson: string;
  phoneNumber: string;
  city: string;
  leadTimeLabel: string;
  paymentTerms: string;
  status: string;
  openPurchaseOrders: number;
  lifetimeSpend: number;
}

export interface PurchaseOrderSummary {
  purchaseOrderId: string;
  purchaseOrderNo: string;
  vendorName: string;
  status: string;
  createdAt: string;
  expectedAt: string | null;
  totalAmount: number;
  lineCount: number;
  orderedUnits: number;
  receivedUnits: number;
}

export interface StockTransferSummary {
  stockTransferId: string;
  transferNo: string;
  fromBranchName: string;
  toBranchName: string;
  status: string;
  createdAt: string;
  expectedAt: string | null;
  units: number;
  requestedBy: string;
}

export interface OperationsHub {
  cash: CashShiftMetrics;
  compliance: ComplianceMetrics;
  cashShifts: CashShiftSummary[];
  moduleGroups: PosModuleGroup[];
}

export interface CashShiftMetrics {
  openRegisters: number;
  todayCashSales: number;
  expectedDrawerCash: number;
  netVariance: number;
}

export interface ComplianceMetrics {
  queuedFbrInvoices: number;
  reportedInvoices: number;
  failedInvoices: number;
  pendingApprovals: number;
  offlineProtectedSales: number;
}

export interface CashShiftSummary {
  cashShiftId: string;
  cashierName: string;
  registerName: string;
  openedAt: string;
  closedAt: string | null;
  openingFloat: number;
  cashSales: number;
  expectedCash: number;
  countedCash: number;
  variance: number;
  status: string;
}

export interface PosModuleGroup {
  title: string;
  description: string;
  modules: PosModuleCard[];
}

export interface PosModuleCard {
  title: string;
  description: string;
  route: string;
  icon: string;
  status: string;
}

export interface WorkspaceStaff {
  userId: string;
  tenantId: string;
  branchId: string;
  branchName: string;
  email: string;
  displayName: string;
  role: string;
}

export interface TenantSummary {
  id: string;
  name: string;
  industryTemplate: string;
  subscriptionPlan: string;
}

export interface CompanySummary {
  id: string;
  name: string;
  baseCurrency: string;
  timeZone: string;
  country: string;
}

export interface BranchSummary {
  id: string;
  code: string;
  name: string;
  warehouseName: string;
  isPrimary: boolean;
}

export interface DashboardOverview {
  sales: DashboardMetric;
  purchases: DashboardMetric;
  grossProfit: DashboardMetric;
  lowStock: DashboardAlert;
  trend: TrendPoint[];
  topSelling: TopSellingItem[];
  recentTransactions: TransactionSummary[];
  branchPerformance: BranchPerformance[];
}

export interface DashboardMetric {
  label: string;
  value: number;
  deltaPercentage: number;
  deltaDirection: 'up' | 'down';
}

export interface DashboardAlert {
  label: string;
  count: number;
  actionLabel: string;
}

export interface TrendPoint {
  label: string;
  value: number;
}

export interface TopSellingItem {
  name: string;
  units: number;
  revenue: number;
}

export interface TransactionSummary {
  referenceNo: string;
  customerName: string;
  amount: number;
  status: string;
  occurredAt: string;
}

export interface BranchPerformance {
  branchName: string;
  percentage: number;
}

export interface InventoryOverview {
  title: string;
  subtitle: string;
  warehouses: string[];
  categories: string[];
  statuses: string[];
  items: InventoryItem[];
}

export interface InventoryImportResult {
  inventory: InventoryOverview;
  importedCount: number;
  createdCount: number;
  updatedCount: number;
  warnings: string[];
}

export interface InventoryItem {
  productId: string;
  sku: string;
  productName: string;
  category: string;
  warehouse: string;
  inHand: number;
  reserved: number;
  available: number;
  unitPrice: number;
  value: number;
  status: string;
  reorderLevel: number;
  visualCode: string;
}

export interface PosTerminal {
  customer: PosCustomer;
  categories: string[];
  products: PosProduct[];
  cart: CartLine[];
  summary: PosSummary;
}

export interface PosCustomer {
  name: string;
  pricingTier: string;
  avatarLetter: string;
}

export interface PosProduct {
  productId: string;
  name: string;
  sku: string;
  category: string;
  unitPrice: number;
  inHand: number;
  isLowStock: boolean;
  isFavorite: boolean;
  visualCode: string;
}

export interface CartLine {
  productId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  allowQuantityEdit: boolean;
}

export interface PosSummary {
  itemCount: number;
  subtotal: number;
  discount: number;
  tax: number;
  total: number;
}

export interface PosCartMutationRequest {
  productId: string;
  quantity: number;
}

export interface PosCheckoutRequest {
  paymentMethod: string;
  receivedAmount: number | null;
  sendToFbr: boolean;
}

export interface PosCheckoutReceipt {
  saleId: string;
  referenceNo: string;
  customerName: string;
  paymentMethod: string;
  cashierName: string;
  occurredAt: string;
  lines: SaleLine[];
  summary: PosSummary;
  receivedAmount: number;
  changeAmount: number;
  fbrStatus: string;
  fbrInvoiceNumber: string | null;
}

export interface SalesHistory {
  items: SalesHistoryItem[];
}

export interface SalesHistoryItem {
  saleId: string;
  referenceNo: string;
  customerName: string;
  amount: number;
  grossProfit: number;
  status: string;
  occurredAt: string;
  itemCount: number;
  discount: number;
  tax: number;
  paymentMethod: string;
  cashierName: string;
  lines: SaleLine[];
  receivedAmount: number;
  changeAmount: number;
  fbrStatus: string;
  fbrInvoiceNumber: string | null;
  fbrErrorMessage: string | null;
  fbrReportedAt: string | null;
}

export interface SaleLine {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface FormBuilder {
  formId: string;
  title: string;
  description: string;
  selectedFieldId: string;
  library: FormLibraryField[];
  canvas: FormCanvasField[];
}

export interface FormLibraryField {
  key: string;
  label: string;
  group: string;
  icon: string;
}

export interface FormCanvasField {
  fieldId: string;
  label: string;
  type: string;
  required: boolean;
  placeholder: string;
  helpText: string | null;
  defaultValue: string | null;
  isReadOnly: boolean;
  minValue: number | null;
  maxValue: number | null;
}

export interface SaveProductRequest {
  sku: string;
  name: string;
  category: string;
  unitPrice: number;
  warehouse: string;
  inHand: number;
  reserved: number;
  reorderLevel: number;
  isFavorite: boolean;
  isQuickSale: boolean;
  visualCode: string;
}

export interface StockAdjustmentRequest {
  productId: string;
  quantityDelta: number;
  reason: string;
}

export interface SaveWorkspaceUserRequest {
  email: string;
  displayName: string;
  role: string;
  branchId: string;
  password: string | null;
}

export interface SaveFormFieldRequest {
  label: string;
  type: string;
  required: boolean;
  placeholder: string;
  helpText: string | null;
  defaultValue: string | null;
  isReadOnly: boolean;
  minValue: number | null;
  maxValue: number | null;
}
