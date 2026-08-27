import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { canAccessModule } from '../../core/role-access';
import { ThemeService } from '../../core/theme.service';
import { WorkspaceApiService } from '../../core/workspace-api.service';

type SupportHighlight = {
  icon: string;
  title: string;
  description: string;
};

type SupportMessage = {
  id: number;
  author: 'bot' | 'user';
  title?: string;
  text: string;
};

const SUPPORT_HIGHLIGHTS: SupportHighlight[] = [
  {
    icon: 'rocket_launch',
    title: 'Onboarding and rollout',
    description: 'Plans, users, branches, counters, printers, and desktop-ready setup guidance.',
  },
  {
    icon: 'inventory_2',
    title: 'Inventory and import help',
    description: 'Excel import, barcode generation, categories, warehouse stock, and opening balance help.',
  },
  {
    icon: 'receipt_long',
    title: 'Printing and FBR support',
    description: 'Popup-print troubleshooting, invoice flow, refund guidance, and offline invoice queue notes.',
  },
];

const SUPPORT_QUICK_PROMPTS = [
  'Set up pricing plans',
  'Import inventory from Excel',
  'Create staff and permissions',
  'Fix printing or popup issue',
  'Explain FBR offline workflow',
];

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);
  private readonly workspaceApi = inject(WorkspaceApiService);
  protected readonly isSidebarOpen = signal(false);
  protected readonly isSupportOpen = signal(false);
  protected readonly supportDraft = signal('');
  protected readonly supportHighlights = SUPPORT_HIGHLIGHTS;
  protected readonly supportQuickPrompts = SUPPORT_QUICK_PROMPTS;
  protected readonly supportMessages = signal<SupportMessage[]>([
    {
      id: 1,
      author: 'bot',
      title: 'Support desk ready',
      text: 'Plans, pricing, inventory import, FBR, users, printers, aur desktop rollout ke bare me pooch sakte hain.',
    },
  ]);
  private supportMessageSequence = 1;

  protected readonly navigationItems = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard', moduleKey: 'dashboard-analytics' },
    { label: 'Sales', icon: 'receipt_long', route: '/sales', moduleKey: 'order-listing' },
    { label: 'POS', icon: 'shopping_cart', route: '/pos', moduleKey: 'pos-checkout' },
    { label: 'Customers', icon: 'group', route: '/customers', moduleKey: 'customer-profiles' },
    { label: 'Procurement', icon: 'local_shipping', route: '/procurement', moduleKey: 'supplier-management' },
    { label: 'Operations Hub', icon: 'policy', route: '/operations', moduleKey: 'fbr-compliance' },
    { label: 'Users & Access', icon: 'badge', route: '/users', moduleKey: 'employee-management' },
    { label: 'Inventory', icon: 'inventory_2', route: '/inventory', moduleKey: 'inventory-core' },
    { label: 'No-Code Builder', icon: 'extension', route: '/form-builder', moduleKey: 'no-code-builder' },
    { label: 'Plans & Modules', icon: 'tune', route: '/plans', moduleKey: 'plan-and-module-control' },
  ];

  protected readonly workspaceContext = toSignal(
    this.workspaceApi.getContext().pipe(catchError(() => of(null))),
    { initialValue: null },
  );

  protected readonly visibleNavigationItems = computed(() => {
    const context = this.workspaceContext();
    if (!context) {
      // Fail closed: until the workspace (roles + entitlements) is confirmed,
      // show nothing rather than leaking owner-only modules.
      return [];
    }

    const enabledModules = new Set(context.access.enabledModules);
    const currentRole = this.authService.currentUser()?.role ?? context.user.role;
    return this.navigationItems.filter((item) =>
      !item.moduleKey || (enabledModules.has(item.moduleKey) && canAccessModule(currentRole, item.moduleKey)));
  });

  constructor() {
    this.authService.hydrateSession().subscribe();
  }

  protected toggleSidebar(): void {
    this.isSidebarOpen.update((value) => !value);
  }

  protected closeSidebar(): void {
    this.isSidebarOpen.set(false);
  }

  protected toggleSupport(): void {
    if (this.isSupportOpen()) {
      this.closeSupport();
      return;
    }

    this.openSupport();
  }

  protected openSupport(): void {
    this.closeSidebar();
    this.isSupportOpen.set(true);
  }

  protected closeSupport(): void {
    this.isSupportOpen.set(false);
  }

  protected sendQuickSupportPrompt(prompt: string): void {
    this.openSupport();
    this.pushSupportMessage('user', prompt);
    const reply = this.buildSupportReply(prompt);
    this.pushSupportMessage('bot', reply.text, reply.title);
  }

  protected sendSupportMessage(): void {
    const text = this.supportDraft().trim();
    if (!text) {
      return;
    }

    this.openSupport();
    this.supportDraft.set('');
    this.pushSupportMessage('user', text);
    const reply = this.buildSupportReply(text);
    this.pushSupportMessage('bot', reply.text, reply.title);
  }

  protected logout(): void {
    this.closeSidebar();
    this.authService.logout();
  }

  protected toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  private pushSupportMessage(author: 'bot' | 'user', text: string, title?: string): void {
    this.supportMessageSequence += 1;
    this.supportMessages.update((messages) => [
      ...messages,
      {
        id: this.supportMessageSequence,
        author,
        title,
        text,
      },
    ]);
  }

  private buildSupportReply(input: string): { title: string; text: string } {
    const message = input.toLowerCase();

    if (message.includes('plan') || message.includes('price') || message.includes('pricing')) {
      return {
        title: 'Plans and pricing',
        text: 'Plans & Modules screen par Retail Starter, Growth, Business, aur Premium presets load karein. Wahan se PKR pricing, included users, branches, aur per-module cost client-wise save ki ja sakti hai.',
      };
    }

    if (message.includes('inventory') || message.includes('excel') || message.includes('import')) {
      return {
        title: 'Inventory import',
        text: 'Inventory screen se Excel ya CSV import use karein. Pehle categories aur core product data set karein, phir stock, barcode, aur warehouse quantities import karke low-stock aur valuation reporting activate karein.',
      };
    }

    if (message.includes('user') || message.includes('employee') || message.includes('permission') || message.includes('role')) {
      return {
        title: 'Users and access',
        text: 'Users & Access module se employee create karein, branch assign karein, aur role-permissions ke zariye module access restrict karein. Yeh client-wise staff control ke liye base module hai.',
      };
    }

    if (message.includes('print') || message.includes('popup') || message.includes('invoice') || message.includes('receipt')) {
      return {
        title: 'Printing help',
        text: 'Browser popup allow karein, phir POS checkout ya sales history se invoice print test karein. Agar popup blocked ho to same browser site settings me popups and redirects allow karke invoice dobara open karein.',
      };
    }

    if (message.includes('fbr') || message.includes('tax') || message.includes('offline')) {
      return {
        title: 'FBR and offline workflow',
        text: 'System ko is tarah design kiya gaya hai ke desktop aur office-network environment me sale queue ho sake. FBR invoice flow ko offline-safe queue ke sath run kar sakte hain aur baad me sync integration attach ki ja sakti hai.',
      };
    }

    return {
      title: 'Support suggestion',
      text: 'Support panel se quick prompts use karein ya Plans, Inventory, Users, aur Operations modules kholo. Main aap ko pricing, setup, printing, import, ya rollout ke next steps bata sakta hoon.',
    };
  }
}
