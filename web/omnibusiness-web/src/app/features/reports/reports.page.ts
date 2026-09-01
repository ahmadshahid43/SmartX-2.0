import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { ReportLedgerEntry, ReportMetric, ReportTableRow, ReportTransaction } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-reports-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  templateUrl: './reports.page.html',
  styleUrl: './reports.page.scss',
})
export class ReportsPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);
  protected readonly activeSection = signal('sales');
  protected readonly registerMode = signal<'transactions' | 'ledger'>('transactions');
  protected readonly query = signal('');
  protected readonly reports = toSignal(
    this.workspaceApi.getReportsHub().pipe(catchError(() => of(null))),
    { initialValue: null },
  );
  protected readonly selectedSection = computed(() =>
    this.reports()?.sections.find((section) => section.key === this.activeSection()) ?? this.reports()?.sections[0] ?? null,
  );
  protected readonly drilldownRows = computed<ReportTableRow[]>(() => {
    const section = this.activeSection();
    const reports = this.reports();
    if (!reports) return [];
    if (section === 'sales') return reports.salesByItem;
    if (section === 'payments') return reports.paymentMethods;
    if (section === 'inventory') return reports.salesByCategory;
    return [];
  });
  protected readonly drilldownTitle = computed(() => {
    switch (this.activeSection()) {
      case 'sales': return 'Sales by item';
      case 'payments': return 'Payment method mix';
      case 'inventory': return 'Sales by category';
      default: return 'Detailed activity';
    }
  });
  protected readonly filteredTransactions = computed<ReportTransaction[]>(() => {
    const query = this.query().trim().toLowerCase();
    return (this.reports()?.transactions ?? []).filter((entry) => !query ||
      [entry.referenceNo, entry.customerName, entry.paymentMethod, entry.fbrStatus, entry.status]
        .some((value) => value.toLowerCase().includes(query)));
  });
  protected readonly filteredLedgerEntries = computed<ReportLedgerEntry[]>(() => {
    const query = this.query().trim().toLowerCase();
    return (this.reports()?.ledgerEntries ?? []).filter((entry) => !query ||
      [entry.referenceNo, entry.party, entry.entryType, entry.status, entry.notes]
        .some((value) => value.toLowerCase().includes(query)));
  });

  protected selectSection(key: string): void {
    this.activeSection.set(key);
    this.registerMode.set(key === 'finance' || key === 'supply' ? 'ledger' : 'transactions');
  }

  protected setRegisterMode(mode: 'transactions' | 'ledger'): void { this.registerMode.set(mode); }
  protected updateQuery(event: Event): void { this.query.set((event.target as HTMLInputElement).value); }

  protected metricValue(metric: ReportMetric): string {
    return metric.format === 'currency'
      ? new Intl.NumberFormat('en-PK', { style: 'currency', currency: 'PKR', maximumFractionDigits: 0 }).format(metric.value)
      : new Intl.NumberFormat('en-PK').format(metric.value);
  }

  protected exportCurrentReport(): void {
    const section = this.selectedSection();
    if (!section) return;
    const lines = [['Metric', 'Value', 'Status'], ...section.metrics.map((metric) => [metric.label, this.metricValue(metric), metric.status])];
    const blob = new Blob([lines.map((line) => line.map((value) => `"${value.replaceAll('"', '""')}"`).join(',')).join('\n')], { type: 'text/csv;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `smartx-${section.key}-report.csv`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  protected printCurrentReport(): void {
    window.print();
  }
}
