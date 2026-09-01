import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth.service';
import { ReceiptPrintService } from '../../core/receipt-print.service';
import { RefundSaleRequest, SalesBookingInsight, SalesHistory, SalesHistoryItem } from '../../core/models';
import { canAccessModule } from '../../core/role-access';
import { WorkspaceApiService } from '../../core/workspace-api.service';

type SalesFilter = 'all' | 'completed' | 'refunded' | 'fbr-pending';

const emptyMetrics = {
  transactionCount: 0,
  netRevenue: 0,
  grossProfit: 0,
  averageTicket: 0,
  refundedCount: 0,
  refundedAmount: 0,
  openBookingCount: 0,
  bookingDueAmount: 0,
  dueTodayBookingCount: 0,
};

@Component({
  selector: 'app-sales-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  templateUrl: './sales.page.html',
  styleUrl: './sales.page.scss',
})
export class SalesPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);
  protected readonly authService = inject(AuthService);
  private readonly receiptPrintService = inject(ReceiptPrintService);

  protected readonly sales = signal<SalesHistory | null>(null);
  protected readonly selectedSaleId = signal('');
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly activeFilter = signal<SalesFilter>('all');
  protected readonly searchTerm = signal('');
  protected readonly refundReason = signal('');
  protected readonly returnToInventory = signal(true);

  protected readonly metrics = computed(() => this.sales()?.metrics ?? emptyMetrics);
  protected readonly paymentMethods = computed(() => this.sales()?.paymentMethods ?? []);
  protected readonly openBookings = computed(() => this.sales()?.openBookings ?? []);
  protected readonly canProcessRefunds = computed(() =>
    canAccessModule(this.authService.currentUser()?.role, 'returns-refunds'));
  protected readonly paymentMixTotal = computed(() =>
    this.paymentMethods().reduce((total, method) => total + method.amount, 0));
  protected readonly filteredSales = computed(() => {
    const query = this.searchTerm().trim().toLowerCase();
    return (this.sales()?.items ?? []).filter((sale) => this.matchesFilter(sale) && this.matchesSearch(sale, query));
  });

  protected readonly selectedSale = computed(() => {
    const filtered = this.filteredSales();
    const selectedId = this.selectedSaleId();
    const fromFiltered = filtered.find((item) => item.saleId === selectedId);
    if (fromFiltered) {
      return fromFiltered;
    }

    const allItems = this.sales()?.items ?? [];
    return filtered[0] ?? allItems.find((item) => item.saleId === selectedId) ?? allItems[0] ?? null;
  });

  protected readonly selectedSaleSubtotal = computed(() => {
    const sale = this.selectedSale();
    return sale ? sale.amount - sale.tax + sale.discount : 0;
  });

  constructor() {
    this.loadSales();
  }

  protected selectSale(saleId: string): void {
    this.selectedSaleId.set(saleId);
    this.successMessage.set('');
  }

  protected setFilter(filter: SalesFilter): void {
    this.activeFilter.set(filter);
  }

  protected updateSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  protected reload(): void {
    this.loadSales(this.selectedSaleId());
  }

  protected submitToFbr(sale: SalesHistoryItem): void {
    if (sale.refundedAmount > 0) {
      this.errorMessage.set('Refunded receipt ko dobara FBR submit nahi kiya ja sakta.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.submitSaleToFbr(sale.saleId).subscribe({
      next: (updatedSale) => {
        const current = this.sales();
        if (current) {
          this.sales.set({
            ...current,
            items: current.items.map((item) => (item.saleId === updatedSale.saleId ? updatedSale : item)),
          });
        }

        this.selectedSaleId.set(updatedSale.saleId);
        this.successMessage.set(`FBR status updated: ${updatedSale.fbrStatus}.`);
        this.loading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'FBR submission status update nahi ho saki.');
        this.loading.set(false);
      },
    });
  }

  protected refundSale(sale: SalesHistoryItem): void {
    if (!this.canProcessRefunds()) {
      this.errorMessage.set('Is account ko refund process karne ki ijazat nahi hai.');
      return;
    }

    if (sale.refundedAmount > 0) {
      this.errorMessage.set('Ye sale pehle hi refund ho chuki hai.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    const request: RefundSaleRequest = {
      reason: this.refundReason().trim() || null,
      returnToInventory: this.returnToInventory(),
    };

    this.workspaceApi.refundSale(sale.saleId, request).subscribe({
      next: () => {
        this.clearRefundDraft();
        this.activeFilter.set('refunded');
        this.loadSales(
          sale.saleId,
          request.returnToInventory
            ? `${sale.referenceNo} refund ho gayi aur stock wapas inventory me chala gaya.`
            : `${sale.referenceNo} refund ho gayi. Inventory quantity unchanged rakhi gayi.`,
        );
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Refund process nahi ho saka.');
        this.loading.set(false);
      },
    });
  }

  protected printInvoice(sale: SalesHistoryItem): void {
    this.errorMessage.set('');
    this.receiptPrintService.printInvoice(sale);
    this.successMessage.set(`Invoice ${sale.referenceNo} print ke liye tayyar hai.`);
  }

  protected printSlip(sale: SalesHistoryItem): void {
    this.errorMessage.set('');
    this.receiptPrintService.printSlip(sale);
    this.successMessage.set(`Receipt slip ${sale.referenceNo} print ke liye tayyar hai.`);
  }

  protected bookingProgress(booking: SalesBookingInsight): number {
    if (booking.totalAmount <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((booking.paidAmount / booking.totalAmount) * 100));
  }

  protected paymentShare(amount: number): number {
    const total = this.paymentMixTotal();
    if (total <= 0) {
      return 0;
    }

    return Math.round((amount / total) * 100);
  }

  protected isFilterActive(filter: SalesFilter): boolean {
    return this.activeFilter() === filter;
  }

  protected statusClass(status: string): string {
    const normalized = status.trim().toLowerCase();

    if (normalized.includes('refund') || normalized.includes('failed') || normalized.includes('reject')) {
      return 'status-chip error';
    }

    if (
      normalized.includes('pending')
      || normalized.includes('queued')
      || normalized.includes('partial')
      || normalized.includes('due')
      || normalized.includes('advance')
    ) {
      return 'status-chip warning';
    }

    if (
      normalized.includes('paid')
      || normalized.includes('submitted')
      || normalized.includes('reported')
      || normalized.includes('completed')
    ) {
      return 'status-chip success';
    }

    return 'status-chip neutral';
  }

  private loadSales(preferredSaleId?: string, successMessage = ''): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.getSalesHistory().subscribe({
      next: (sales) => {
        this.sales.set(sales);
        const selected = sales.items.find((item) => item.saleId === preferredSaleId) ?? this.filteredFallback(sales);
        this.selectedSaleId.set(selected?.saleId ?? '');
        this.successMessage.set(successMessage);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Sales history load nahi ho saki. API run aur login session check karein.');
        this.loading.set(false);
      },
    });
  }

  private filteredFallback(sales: SalesHistory): SalesHistoryItem | null {
    const filtered = sales.items.filter((sale) =>
      this.matchesFilter(sale) && this.matchesSearch(sale, this.searchTerm().trim().toLowerCase()));

    return filtered[0] ?? sales.items[0] ?? null;
  }

  private clearRefundDraft(): void {
    this.refundReason.set('');
    this.returnToInventory.set(true);
  }

  private matchesFilter(sale: SalesHistoryItem): boolean {
    switch (this.activeFilter()) {
      case 'completed':
        return sale.refundedAmount <= 0;
      case 'refunded':
        return sale.refundedAmount > 0;
      case 'fbr-pending':
        return sale.fbrStatus.toLowerCase().includes('pending') || sale.fbrStatus.toLowerCase().includes('queued');
      default:
        return true;
    }
  }

  private matchesSearch(sale: SalesHistoryItem, query: string): boolean {
    if (!query) {
      return true;
    }

    return [
      sale.referenceNo,
      sale.customerName,
      sale.paymentMethod,
      sale.cashierName,
      sale.status,
      sale.fbrStatus,
      ...sale.lines.map((line) => `${line.name} ${line.sku}`),
    ]
      .join(' ')
      .toLowerCase()
      .includes(query);
  }
}
