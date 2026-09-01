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
  notes: string;
}

export interface OperationsHub {
  cash: CashShiftMetrics;
  compliance: ComplianceMetrics;
  cashShifts: CashShiftSummary[];
  moduleGroups: PosModuleGroup[];
}

export interface WarehouseHub {
  metrics: WarehouseMetrics;
  branches: string[];
  warehouses: string[];
  stockTransfers: StockTransferSummary[];
  goodsReceipts: GoodsReceiptSummary[];
  gatePasses: GatePassSummary[];
}

export interface WarehouseMetrics {
  activeTransfers: number;
  pendingReceipts: number;
  openGatePasses: number;
  unitsInMotion: number;
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

export interface GoodsReceiptSummary {
  goodsReceiptId: string;
  receiptNo: string;
  purchaseOrderNo: string;
  vendorName: string;
  warehouseName: string;
  status: string;
  receivedAt: string;
  receivedBy: string;
  lineCount: number;
  receivedUnits: number;
  varianceUnits: number;
  notes: string;
}

export interface GatePassSummary {
  gatePassId: string;
  gatePassNo: string;
  movementType: string;
  warehouseName: string;
  destinationName: string;
  referenceNo: string;
  status: string;
  issuedAt: string;
  issuedBy: string;
  units: number;
  notes: string;
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

export interface ReportsHub {
  generatedAt: string;
  sections: ReportSection[];
  salesByItem: ReportTableRow[];
  salesByCategory: ReportTableRow[];
  paymentMethods: ReportTableRow[];
  ledgerEntries: ReportLedgerEntry[];
  transactions: ReportTransaction[];
}

export interface ReportLedgerEntry {
  occurredAt: string;
  referenceNo: string;
  party: string;
  entryType: string;
  debit: number;
  credit: number;
  balance: number;
  status: string;
  notes: string;
}

export interface ReportTransaction {
  occurredAt: string;
  referenceNo: string;
  customerName: string;
  paymentMethod: string;
  itemCount: number;
  subtotal: number;
  discount: number;
  tax: number;
  total: number;
  grossProfit: number;
  fbrStatus: string;
  status: string;
}

export interface ReportSection {
  key: string;
  title: string;
  description: string;
  icon: string;
  metrics: ReportMetric[];
}

export interface ReportMetric {
  key: string;
  label: string;
  value: number;
  format: 'currency' | 'number';
  status: 'ready' | 'attention' | 'risk' | 'data-required' | string;
}

export interface ReportTableRow {
  label: string;
  amount: number;
  count: number;
  secondaryLabel: string;
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
  metrics: InventoryMetrics;
  lowStockItems: InventoryLowStockItem[];
  warehouseSummaries: InventoryWarehouseSummary[];
  categorySummaries: InventoryCategorySummary[];
  usageInsights: InventoryUsageInsight[];
  barcodeQueue: InventoryBarcodeQueueItem[];
  recentStockTakes: StockTakeSummary[];
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
  isFavorite: boolean;
  isQuickSale: boolean;
}

export interface InventoryMetrics {
  totalProducts: number;
  lowStockCount: number;
  totalValue: number;
  warehouseCount: number;
  categoryCount: number;
  stockTakeCount30Days: number;
  turnoverRatio30Days: number;
}

export interface InventoryLowStockItem {
  productId: string;
  sku: string;
  productName: string;
  warehouse: string;
  available: number;
  reorderLevel: number;
  shortfallUnits: number;
}

export interface InventoryWarehouseSummary {
  warehouse: string;
  productCount: number;
  inHandUnits: number;
  availableUnits: number;
  stockValue: number;
}

export interface InventoryCategorySummary {
  category: string;
  productCount: number;
  availableUnits: number;
  stockValue: number;
  sharePercentage: number;
}

export interface InventoryUsageInsight {
  productId: string;
  sku: string;
  productName: string;
  soldUnits30Days: number;
  netAdjustment30Days: number;
  turnoverRatio30Days: number;
  coverageLabel: string;
}

export interface InventoryBarcodeQueueItem {
  productId: string;
  sku: string;
  productName: string;
  category: string;
  visualCode: string;
  isFavorite: boolean;
  isQuickSale: boolean;
}

export interface StockTakeSummary {
  id: string;
  productId: string;
  sku: string;
  productName: string;
  warehouse: string;
  systemQuantity: number;
  countedQuantity: number;
  varianceQuantity: number;
  status: string;
  countedBy: string;
  notes: string;
  countedAt: string;
}

export interface PosTerminal {
  customer: PosCustomer;
  categories: string[];
  products: PosProduct[];
  cart: CartLine[];
  summary: PosSummary;
  heldOrders: PosHeldOrder[];
  bookings: PosBookingOrder[];
  metrics: PosWorkflowMetrics;
  paymentMethods: string[];
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

export interface PosWorkflowMetrics {
  heldOrderCount: number;
  bookingCount: number;
  bookingDueAmount: number;
  pendingCollectionCount: number;
}

export interface PosHeldOrder {
  id: string;
  ticketNo: string;
  customerName: string;
  pricingTier: string;
  heldBy: string;
  heldAt: string;
  itemCount: number;
  total: number;
  lines: CartLine[];
  notes: string;
}

export interface PosBookingOrder {
  id: string;
  bookingNo: string;
  customerName: string;
  phoneNumber: string | null;
  email: string | null;
  status: string;
  createdAt: string;
  dueAt: string | null;
  bookedBy: string;
  itemCount: number;
  subtotal: number;
  discount: number;
  tax: number;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  paymentStatus: string;
  lines: SaleLine[];
  payments: PosPaymentLine[];
  notes: string;
}

export interface PosPaymentLine {
  method: string;
  amount: number;
  referenceNo: string | null;
}

export interface PosCartMutationRequest {
  productId: string;
  quantity: number;
}

export interface SelectPosCustomerRequest {
  customerId: string | null;
  customerName?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
}

export interface PosCheckoutRequest {
  paymentMethod: string;
  receivedAmount: number | null;
  sendToFbr: boolean;
  payments?: PosPaymentLineRequest[] | null;
  taxRatePercent?: number | null;
  taxExempt?: boolean;
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
  paidAmount: number;
  balanceAmount: number;
  paymentStatus: string;
  payments: PosPaymentLine[];
}

export interface PosWorkflowAction {
  message: string;
  terminal: PosTerminal;
  booking: PosBookingOrder | null;
  receipt: PosCheckoutReceipt | null;
}

export interface SalesHistory {
  metrics: SalesHistoryMetrics;
  paymentMethods: SalesPaymentMethodSummary[];
  openBookings: SalesBookingInsight[];
  items: SalesHistoryItem[];
}

export interface SalesHistoryMetrics {
  transactionCount: number;
  netRevenue: number;
  grossProfit: number;
  averageTicket: number;
  refundedCount: number;
  refundedAmount: number;
  openBookingCount: number;
  bookingDueAmount: number;
  dueTodayBookingCount: number;
}

export interface SalesPaymentMethodSummary {
  method: string;
  amount: number;
  transactionCount: number;
}

export interface SalesBookingInsight {
  bookingId: string;
  bookingNo: string;
  customerName: string;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  paymentStatus: string;
  createdAt: string;
  dueAt: string | null;
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
  paidAmount: number;
  balanceAmount: number;
  paymentStatus: string;
  payments: PosPaymentLine[];
  netAmount: number;
  refundedAmount: number;
  refundedAt: string | null;
  refundedBy: string | null;
  refundReason: string | null;
  inventoryReturned: boolean;
}

export interface PosPaymentLineRequest {
  method: string;
  amount: number;
  referenceNo: string | null;
}

export interface CreateHeldOrderRequest {
  notes: string | null;
}

export interface CreateBookingOrderRequest {
  customerName: string;
  phoneNumber: string | null;
  email: string | null;
  dueAt: string | null;
  notes: string | null;
  payments?: PosPaymentLineRequest[] | null;
}

export interface CollectBookingPaymentRequest {
  amount: number;
  paymentMethod: string;
  referenceNo: string | null;
  notes: string | null;
}

export interface CompleteBookingRequest {
  sendToFbr: boolean;
}

export interface RefundSaleRequest {
  reason: string | null;
  returnToInventory: boolean;
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

export interface SaveStockTakeRequest {
  productId: string;
  countedQuantity: number;
  notes: string | null;
}

export interface SaveStockTransferRequest {
  fromBranchName: string;
  toBranchName: string;
  units: number;
  expectedAt: string | null;
  status: string;
  notes: string | null;
}

export interface SaveGoodsReceiptRequest {
  purchaseOrderNo: string;
  vendorName: string;
  warehouseName: string;
  lineCount: number;
  receivedUnits: number;
  varianceUnits: number;
  status: string;
  notes: string | null;
}

export interface SaveGatePassRequest {
  movementType: string;
  warehouseName: string;
  destinationName: string;
  referenceNo: string;
  units: number;
  status: string;
  notes: string | null;
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
