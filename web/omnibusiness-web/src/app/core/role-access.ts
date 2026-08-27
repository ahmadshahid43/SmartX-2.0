const ownerRole = 'owner';
const managerRole = 'manager';
const cashierRole = 'cashier';
const backOfficeRole = 'back office';

const moduleRoleMap = new Map<string, Set<string>>([
  ['dashboard-analytics', new Set([ownerRole, managerRole, backOfficeRole])],
  ['pos-checkout', new Set([ownerRole, managerRole, cashierRole])],
  ['counter-orders', new Set([ownerRole, managerRole, cashierRole])],
  ['book-orders', new Set([ownerRole, managerRole, cashierRole])],
  ['hold-and-resume', new Set([ownerRole, managerRole, cashierRole])],
  ['customer-profiles', new Set([ownerRole, managerRole, cashierRole, backOfficeRole])],
  ['split-payments', new Set([ownerRole, managerRole, cashierRole])],
  ['late-payments', new Set([ownerRole, managerRole])],
  ['service-cards', new Set([ownerRole, managerRole])],
  ['returns-refunds', new Set([ownerRole, managerRole])],
  ['inventory-core', new Set([ownerRole, managerRole, backOfficeRole])],
  ['trade-in', new Set([ownerRole, managerRole])],
  ['stock-take', new Set([ownerRole, managerRole, backOfficeRole])],
  ['grn-receiving', new Set([ownerRole, managerRole, backOfficeRole])],
  ['warehouse-reports', new Set([ownerRole, managerRole, backOfficeRole])],
  ['barcode-suite', new Set([ownerRole, managerRole, backOfficeRole])],
  ['supplier-management', new Set([ownerRole, managerRole, backOfficeRole])],
  ['purchase-orders', new Set([ownerRole, managerRole, backOfficeRole])],
  ['expense-management', new Set([ownerRole, managerRole, backOfficeRole])],
  ['ledger-accounting', new Set([ownerRole, managerRole, backOfficeRole])],
  ['profit-loss', new Set([ownerRole, managerRole])],
  ['fbr-compliance', new Set([ownerRole, managerRole, backOfficeRole])],
  ['pos-configuration', new Set([ownerRole, managerRole])],
  ['order-listing', new Set([ownerRole, managerRole, cashierRole, backOfficeRole])],
  ['booking-analytics', new Set([ownerRole, managerRole])],
  ['reporting-suite', new Set([ownerRole, managerRole, backOfficeRole])],
  ['tax-and-refund-reporting', new Set([ownerRole, managerRole, backOfficeRole])],
  ['expiry-and-usage-reporting', new Set([ownerRole, managerRole, backOfficeRole])],
  ['social-publishing', new Set([ownerRole, managerRole])],
  ['customer-notifications', new Set([ownerRole, managerRole, backOfficeRole])],
  ['employee-management', new Set([ownerRole])],
  ['role-permissions', new Set([ownerRole])],
  ['no-code-builder', new Set([ownerRole])],
  ['plan-and-module-control', new Set([ownerRole])],
]);

export function normalizeRole(role: string | null | undefined): string {
  return (role ?? '').trim().toLowerCase() || cashierRole;
}

export function canAccessModule(role: string | null | undefined, moduleKey: string): boolean {
  const normalizedRole = normalizeRole(role);
  if (normalizedRole === ownerRole) {
    return true;
  }

  const normalizedModuleKey = moduleKey.trim().toLowerCase();
  return moduleRoleMap.get(normalizedModuleKey)?.has(normalizedRole) ?? false;
}

export function filterEnabledModules(role: string | null | undefined, moduleKeys: string[]): string[] {
  return moduleKeys.filter((moduleKey) => canAccessModule(role, moduleKey));
}

// Ordered landing candidates: the first route whose module the role can reach
// becomes the post-login landing page, so a cashier goes straight to POS instead
// of hitting /dashboard and being bounced by the module guard.
const landingCandidates: ReadonlyArray<{ moduleKey: string; route: string }> = [
  { moduleKey: 'dashboard-analytics', route: '/dashboard' },
  { moduleKey: 'pos-checkout', route: '/pos' },
  { moduleKey: 'order-listing', route: '/sales' },
  { moduleKey: 'inventory-core', route: '/inventory' },
  { moduleKey: 'customer-profiles', route: '/customers' },
];

export function resolveLandingRoute(role: string | null | undefined): string {
  const match = landingCandidates.find((candidate) => canAccessModule(role, candidate.moduleKey));
  return match?.route ?? '/pos';
}
