import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ModuleSettings,
  ModuleSettingsGroup,
  SaveModuleEntitlementRequest,
  SaveModuleSettingsRequest,
} from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

type PlanPresetDefinition = {
  code: string;
  name: string;
  theme: 'starter' | 'growth' | 'business' | 'premium';
  badge?: string;
  bestFor: string;
  supportModel: string;
  counters: string;
  deviceMode: string;
  includedUsers: number;
  includedBranches: number;
  targetMonthlyPrice: number;
  modules: string[];
  highlights: string[];
};

type PlanPresetCard = PlanPresetDefinition & {
  moduleCount: number;
  moduleCost: number;
  baseComponent: number;
};

const PLAN_PRESETS: PlanPresetDefinition[] = [
  {
    code: 'starter',
    name: 'Retail Starter',
    theme: 'starter',
    bestFor: 'Single-counter retail, pharmacy, mobile shop, mini mart',
    supportModel: 'Setup checklist + chatbot support',
    counters: '1 counter + walk-in billing',
    deviceMode: 'Web + desktop-ready offline workflow',
    includedUsers: 3,
    includedBranches: 1,
    targetMonthlyPrice: 3499,
    modules: [
      'dashboard-analytics',
      'pos-checkout',
      'customer-profiles',
      'inventory-core',
      'order-listing',
      'employee-management',
      'role-permissions',
      'plan-and-module-control',
    ],
    highlights: ['Quick checkout', 'Excel inventory import', 'User roles', 'Invoice printing'],
  },
  {
    code: 'growth',
    name: 'Growth',
    theme: 'growth',
    bestFor: 'Busy counters, stock intake, purchasing, and barcode control',
    supportModel: 'Onboarding support + printer guidance',
    counters: '2 counters + split tender ops',
    deviceMode: 'Web POS + local office setup',
    includedUsers: 8,
    includedBranches: 1,
    targetMonthlyPrice: 5999,
    modules: [
      'dashboard-analytics',
      'pos-checkout',
      'counter-orders',
      'customer-profiles',
      'split-payments',
      'inventory-core',
      'supplier-management',
      'purchase-orders',
      'grn-receiving',
      'barcode-suite',
      'reporting-suite',
      'employee-management',
      'role-permissions',
      'plan-and-module-control',
    ],
    highlights: ['Counter control', 'Supplier + PO flow', 'Barcode suite', 'Operational reporting'],
  },
  {
    code: 'business',
    name: 'Business',
    theme: 'business',
    badge: 'Most Popular',
    bestFor: 'FBR-aware retail with warehouse, refunds, and custom commercial packages',
    supportModel: 'Priority support + rollout playbook',
    counters: '4 counters + branch workflow',
    deviceMode: 'Web + desktop + offline-safe invoice queue',
    includedUsers: 20,
    includedBranches: 3,
    targetMonthlyPrice: 8999,
    modules: [
      'dashboard-analytics',
      'pos-checkout',
      'counter-orders',
      'hold-and-resume',
      'customer-profiles',
      'split-payments',
      'returns-refunds',
      'inventory-core',
      'stock-take',
      'warehouse-reports',
      'barcode-suite',
      'supplier-management',
      'purchase-orders',
      'grn-receiving',
      'reporting-suite',
      'fbr-compliance',
      'customer-notifications',
      'employee-management',
      'role-permissions',
      'no-code-builder',
      'pos-configuration',
      'plan-and-module-control',
    ],
    highlights: ['FBR queue handling', 'Warehouse reports', 'Returns and refunds', 'No-code setup'],
  },
  {
    code: 'premium',
    name: 'Premium',
    theme: 'premium',
    bestFor: 'Advanced retail, service, social selling, trade-in, and finance workflows',
    supportModel: 'Dedicated support lane + deployment consulting',
    counters: 'Multi-counter + multi-branch operations',
    deviceMode: 'Full omnistack rollout with advanced workflows',
    includedUsers: 50,
    includedBranches: 10,
    targetMonthlyPrice: 14999,
    modules: [
      'dashboard-analytics',
      'pos-checkout',
      'counter-orders',
      'book-orders',
      'hold-and-resume',
      'customer-profiles',
      'split-payments',
      'late-payments',
      'service-cards',
      'returns-refunds',
      'inventory-core',
      'trade-in',
      'stock-take',
      'grn-receiving',
      'warehouse-reports',
      'barcode-suite',
      'supplier-management',
      'purchase-orders',
      'expense-management',
      'ledger-accounting',
      'profit-loss',
      'fbr-compliance',
      'pos-configuration',
      'order-listing',
      'booking-analytics',
      'reporting-suite',
      'tax-and-refund-reporting',
      'expiry-and-usage-reporting',
      'social-publishing',
      'customer-notifications',
      'employee-management',
      'role-permissions',
      'no-code-builder',
      'plan-and-module-control',
    ],
    highlights: ['Installment payments', 'Service cards', 'Trade-in workflow', 'Social publishing'],
  },
];

const MARKET_BENCHMARKS = [
  'Pakistan retail starter packages usually land around module-led entry pricing.',
  'Growth tiers are tuned for purchasing, barcode, and multi-counter operations.',
  'Business tiers cover FBR, warehouse, returns, and client-specific setup control.',
  'Premium is positioned for multi-branch rollout and advanced ERP + POS workflows.',
];

@Component({
  selector: 'app-plans-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './plans.page.html',
  styleUrl: './plans.page.scss',
})
export class PlansPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly settings = signal<ModuleSettings | null>(null);
  protected readonly form = signal<SaveModuleSettingsRequest | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly marketBenchmarks = MARKET_BENCHMARKS;
  protected readonly activePlanCode = computed(() => this.form()?.planCode.trim().toLowerCase() ?? '');

  protected readonly moduleGroups = computed<ModuleSettingsGroup[]>(() => {
    const settings = this.settings();
    const form = this.form();
    if (!settings || !form) {
      return [];
    }

    const moduleState = new Map(form.modules.map((module) => [module.moduleKey, module] as const));

    return settings.groups.map((group) => ({
      ...group,
      modules: group.modules.map((module) => {
        const current = moduleState.get(module.moduleKey);
        return {
          ...module,
          isEnabled: current?.enabled ?? module.isEnabled,
          addOnMonthlyPrice: current?.addOnMonthlyPrice ?? module.addOnMonthlyPrice,
        };
      }),
    }));
  });

  protected readonly enabledModulesCount = computed(() => {
    return this.form()?.modules.filter((module) => module.enabled).length ?? 0;
  });

  protected readonly estimatedMonthlyTotal = computed(() => {
    const form = this.form();
    if (!form) {
      return 0;
    }

    const addOnTotal = form.modules
      .filter((module) => module.enabled)
      .reduce((sum, module) => sum + module.addOnMonthlyPrice, 0);

    return form.baseMonthlyPrice + addOnTotal;
  });

  protected readonly presetCards = computed<PlanPresetCard[]>(() => {
    const priceMap = new Map(
      this.moduleGroups()
        .flatMap((group) => group.modules)
        .map((module) => [module.moduleKey, module.addOnMonthlyPrice] as const),
    );

    return PLAN_PRESETS.map((preset) => {
      const moduleCost = preset.modules.reduce(
        (sum, moduleKey) => sum + (priceMap.get(moduleKey) ?? 0),
        0,
      );

      return {
        ...preset,
        moduleCount: preset.modules.length,
        moduleCost,
        baseComponent: Math.max(preset.targetMonthlyPrice - moduleCost, 0),
      };
    });
  });

  constructor() {
    this.loadSettings();
  }

  protected reload(): void {
    this.loadSettings();
  }

  protected updatePlanField(
    key: 'planName' | 'planCode' | 'currency',
    value: string,
  ): void {
    const current = this.form();
    if (!current) {
      return;
    }

    this.form.set({
      ...current,
      [key]: value,
    });
  }

  protected updateNumericField(
    key: 'baseMonthlyPrice' | 'includedUsers' | 'includedBranches',
    rawValue: string,
  ): void {
    const current = this.form();
    if (!current) {
      return;
    }

    const parsed = Number(rawValue);
    const value = Number.isFinite(parsed) ? parsed : 0;

    this.form.set({
      ...current,
      [key]: value,
    });
  }

  protected updateCustomModuleOverride(enabled: boolean): void {
    const current = this.form();
    if (!current) {
      return;
    }

    this.form.set({
      ...current,
      allowCustomModuleOverrides: enabled,
    });
  }

  protected toggleModule(moduleKey: string, enabled: boolean): void {
    this.updateModuleState(moduleKey, (module) => ({
      ...module,
      enabled,
    }));
  }

  protected applyPreset(planCode: string): void {
    const current = this.form();
    const settings = this.settings();
    const preset = PLAN_PRESETS.find((item) => item.code === planCode);
    if (!current || !settings || !preset) {
      return;
    }

    const priceMap = new Map(
      settings.groups
        .flatMap((group) => group.modules)
        .map((module) => [module.moduleKey, module.addOnMonthlyPrice] as const),
    );
    const enabledModules = new Set(preset.modules);
    const moduleCost = preset.modules.reduce(
      (sum, moduleKey) => sum + (priceMap.get(moduleKey) ?? 0),
      0,
    );

    this.form.set({
      ...current,
      planCode: preset.code,
      planName: preset.name,
      currency: 'PKR',
      baseMonthlyPrice: Math.max(preset.targetMonthlyPrice - moduleCost, 0),
      includedUsers: preset.includedUsers,
      includedBranches: preset.includedBranches,
      allowCustomModuleOverrides: true,
      modules: settings.groups
        .flatMap((group) => group.modules)
        .map<SaveModuleEntitlementRequest>((module) => ({
          moduleKey: module.moduleKey,
          enabled: enabledModules.has(module.moduleKey),
          addOnMonthlyPrice: priceMap.get(module.moduleKey) ?? module.addOnMonthlyPrice,
        })),
    });

    this.errorMessage.set('');
    this.successMessage.set(`${preset.name} package loaded. Review modules and click Save Plan Setup when ready.`);
  }

  protected updateModulePrice(moduleKey: string, rawValue: string): void {
    const parsed = Number(rawValue);
    const addOnMonthlyPrice = Number.isFinite(parsed) && parsed > 0 ? parsed : 0;

    this.updateModuleState(moduleKey, (module) => ({
      ...module,
      addOnMonthlyPrice,
    }));
  }

  protected saveSettings(): void {
    const request = this.form();
    if (!request) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.updateModuleSettings(request).subscribe({
      next: (settings) => {
        this.hydrateForm(settings);
        this.saving.set(false);
        this.successMessage.set('Plan and module settings saved successfully.');
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Module settings save nahi ho sakin.');
        this.saving.set(false);
      },
    });
  }

  private loadSettings(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.getModuleSettings().subscribe({
      next: (settings) => {
        this.hydrateForm(settings);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Plans and modules load nahi ho sake. API aur login session dobara check karein.');
        this.loading.set(false);
      },
    });
  }

  private hydrateForm(settings: ModuleSettings): void {
    this.settings.set(settings);
    this.form.set({
      planCode: settings.access.planCode,
      planName: settings.access.planName,
      currency: settings.access.currency,
      baseMonthlyPrice: settings.access.baseMonthlyPrice,
      includedUsers: settings.access.includedUsers,
      includedBranches: settings.access.includedBranches,
      allowCustomModuleOverrides: settings.access.allowCustomModuleOverrides,
      modules: settings.groups
        .flatMap((group) => group.modules)
        .map<SaveModuleEntitlementRequest>((module) => ({
          moduleKey: module.moduleKey,
          enabled: module.isEnabled,
          addOnMonthlyPrice: module.addOnMonthlyPrice,
        })),
    });
  }

  private updateModuleState(
    moduleKey: string,
    updater: (module: SaveModuleEntitlementRequest) => SaveModuleEntitlementRequest,
  ): void {
    const current = this.form();
    if (!current) {
      return;
    }

    this.form.set({
      ...current,
      modules: current.modules.map((module) =>
        module.moduleKey === moduleKey ? updater(module) : module),
    });
  }
}
