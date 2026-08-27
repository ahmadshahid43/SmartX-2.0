import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ReceiptPrintService } from '../../core/receipt-print.service';
import { SalesHistory, SalesHistoryItem } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-sales-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  templateUrl: './sales.page.html',
  styleUrl: './sales.page.scss',
})
export class SalesPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);
  private readonly receiptPrintService = inject(ReceiptPrintService);

  protected readonly sales = signal<SalesHistory | null>(null);
  protected readonly selectedSaleId = signal('');
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');

  protected readonly selectedSale = computed(() => {
    const items = this.sales()?.items ?? [];
    return items.find((item) => item.saleId === this.selectedSaleId()) ?? items[0] ?? null;
  });

  protected readonly summary = computed(() => {
    const items = this.sales()?.items ?? [];
    return {
      transactionCount: items.length,
      revenue: items.reduce((total, item) => total + item.amount, 0),
      grossProfit: items.reduce((total, item) => total + item.grossProfit, 0),
      averageTicket: items.length === 0 ? 0 : items.reduce((total, item) => total + item.amount, 0) / items.length,
    };
  });

  constructor() {
    this.loadSales();
  }

  protected selectSale(saleId: string): void {
    this.selectedSaleId.set(saleId);
  }

  protected reload(): void {
    this.loadSales();
  }

  protected submitToFbr(sale: SalesHistoryItem): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.submitSaleToFbr(sale.saleId).subscribe({
      next: (updatedSale) => {
        const current = this.sales();
        if (current) {
          this.sales.set({
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

  private loadSales(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.getSalesHistory().subscribe({
      next: (sales) => {
        this.sales.set(sales);
        this.selectedSaleId.set(sales.items[0]?.saleId ?? '');
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Sales history load nahi ho saki. API run aur login session check karein.');
        this.loading.set(false);
      },
    });
  }
}
