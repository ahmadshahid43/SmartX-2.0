import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { PosCheckoutReceipt, PosPaymentLine, SalesHistoryItem, SaleLine, WorkspaceContext } from './models';
import { WorkspaceApiService } from './workspace-api.service';

type PrintableSale = PosCheckoutReceipt | SalesHistoryItem;
export type PrintVariant = 'invoice' | 'slip';

interface PrintBrandProfile {
  platformName: string;
  storeName: string;
  storeTagline: string;
  location: string;
  contact: string | null;
}

interface NormalizedPrintableSale {
  referenceNo: string;
  customerName: string;
  paymentMethod: string;
  cashierName: string;
  occurredAt: string;
  lines: SaleLine[];
  subtotal: number;
  discount: number;
  tax: number;
  total: number;
  receivedAmount: number;
  changeAmount: number;
  fbrStatus: string;
  fbrInvoiceNumber: string | null;
  paymentStatus: string;
  payments: PosPaymentLine[];
}

export interface ReceiptPrintJob {
  id: string;
  title: string;
  variant: PrintVariant;
  markup: string;
}

const PLATFORM_NAME = 'SmartX ERP';

const FALLBACK_BRAND: PrintBrandProfile = {
  platformName: PLATFORM_NAME,
  storeName: 'SmartX Workspace',
  storeTagline: 'Retail & pharmacy point of sale',
  location: 'Pakistan',
  contact: null,
};

@Injectable({ providedIn: 'root' })
export class ReceiptPrintService {
  private readonly workspaceApi = inject(WorkspaceApiService);

  readonly activeJob = signal<ReceiptPrintJob | null>(null);
  private readonly context = signal<WorkspaceContext | null>(null);
  private jobSequence = 0;

  private readonly brand = computed<PrintBrandProfile>(() => {
    const context = this.context();
    if (!context) {
      return FALLBACK_BRAND;
    }

    const primaryBranch = context.branches.find((branch) => branch.isPrimary) ?? context.branches[0] ?? null;
    const locationParts = [primaryBranch?.name, primaryBranch?.warehouseName, context.company.country]
      .filter((part): part is string => !!part && part.trim().length > 0);

    return {
      platformName: PLATFORM_NAME,
      storeName: context.company.name || context.tenant.name || FALLBACK_BRAND.storeName,
      storeTagline: context.tenant.industryTemplate || FALLBACK_BRAND.storeTagline,
      location: locationParts.length > 0 ? locationParts.join(' • ') : FALLBACK_BRAND.location,
      contact: null,
    };
  });

  constructor() {
    // Load the workspace once so receipts show the real business, not a placeholder.
    this.workspaceApi
      .getContext()
      .pipe(catchError(() => of(null)))
      .subscribe((context) => this.context.set(context));
  }

  printInvoice(sale: PrintableSale): void {
    this.queuePrint(this.normalizeSale(sale), 'invoice');
  }

  printSlip(sale: PrintableSale): void {
    this.queuePrint(this.normalizeSale(sale), 'slip');
  }

  clearActiveJob(): void {
    this.activeJob.set(null);
  }

  setPrintMode(isActive: boolean): void {
    document.body.classList.toggle('printing-receipt', isActive);
  }

  private queuePrint(sale: NormalizedPrintableSale, variant: PrintVariant): void {
    this.jobSequence += 1;
    this.activeJob.set({
      id: `${sale.referenceNo}-${variant}-${this.jobSequence}`,
      title: `${sale.referenceNo} ${variant === 'slip' ? 'Receipt Slip' : 'Invoice'}`,
      variant,
      markup: this.buildMarkup(sale, variant),
    });
  }

  private normalizeSale(sale: PrintableSale): NormalizedPrintableSale {
    const subtotal = 'summary' in sale ? sale.summary.subtotal : sale.amount - sale.tax + sale.discount;
    const total = 'summary' in sale ? sale.summary.total : sale.amount;

    return {
      referenceNo: sale.referenceNo,
      customerName: sale.customerName,
      paymentMethod: sale.paymentMethod,
      cashierName: 'cashierName' in sale ? sale.cashierName : '',
      occurredAt: new Date(sale.occurredAt).toLocaleString('en-PK', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }),
      lines: sale.lines,
      subtotal,
      discount: 'summary' in sale ? sale.summary.discount : sale.discount,
      tax: 'summary' in sale ? sale.summary.tax : sale.tax,
      total,
      receivedAmount: sale.receivedAmount,
      changeAmount: sale.changeAmount,
      fbrStatus: sale.fbrStatus,
      fbrInvoiceNumber: sale.fbrInvoiceNumber,
      paymentStatus: sale.paymentStatus,
      payments: sale.payments,
    };
  }

  private buildMarkup(sale: NormalizedPrintableSale, variant: PrintVariant): string {
    const isSlip = variant === 'slip';
    const brand = this.brand();
    const currency = new Intl.NumberFormat('en-PK', {
      style: 'currency',
      currency: 'PKR',
      maximumFractionDigits: 0,
    });
    const lineRows = sale.lines
      .map(
        (line) => `
          <tr>
            <td>
              <strong>${this.escapeHtml(line.name)}</strong>
              <div class="print-sku">${this.escapeHtml(line.sku)}</div>
            </td>
            <td class="center">${line.quantity}</td>
            <td class="right">${currency.format(line.unitPrice)}</td>
            <td class="right">${currency.format(line.lineTotal)}</td>
          </tr>`,
      )
      .join('');

    const contactLine = brand.contact
      ? `<p>${this.escapeHtml(brand.contact)}</p>`
      : '';
    const paymentBreakdown = sale.payments.length > 0
      ? `
          <section class="print-payment-breakdown">
            <div class="print-meta-label">Payment Breakdown</div>
            ${sale.payments
              .map(
                (payment) => `
                  <div class="print-payment-row">
                    <span>${this.escapeHtml(payment.method)}${payment.referenceNo ? ` • ${this.escapeHtml(payment.referenceNo)}` : ''}</span>
                    <strong>${currency.format(payment.amount)}</strong>
                  </div>`,
              )
              .join('')}
          </section>`
      : '';

    return `
      <main class="print-sheet ${isSlip ? 'is-slip' : 'is-invoice'}">
        <section class="print-brand">
          <div class="print-brand-top">
            <div>
              <div class="print-brand-platform">${this.escapeHtml(brand.platformName)}</div>
              <h1>${this.escapeHtml(brand.storeName)}</h1>
            </div>
            <div class="print-brand-badge">${isSlip ? 'Thermal Slip' : 'Tax Invoice'}</div>
          </div>
          <p class="print-brand-subtitle">${this.escapeHtml(brand.storeTagline)}</p>
          <p>${this.escapeHtml(brand.location)}</p>
          ${contactLine}
          <p class="print-brand-license">Powered by ${this.escapeHtml(brand.platformName)}</p>
        </section>

        <section class="print-meta-grid">
          <div class="print-meta-block">
            <div class="print-meta-label">Reference</div>
            <div>${this.escapeHtml(sale.referenceNo)}</div>
            <p class="print-meta">${this.escapeHtml(sale.customerName)}</p>
          </div>
          <div class="print-meta-block">
            <div class="print-meta-label">Payment</div>
            <div>${this.escapeHtml(sale.paymentMethod)}</div>
            <p class="print-meta">${this.escapeHtml(sale.paymentStatus)} • ${this.escapeHtml(sale.occurredAt)}</p>
          </div>
          <div class="print-meta-block">
            <div class="print-meta-label">Cashier</div>
            <div>${this.escapeHtml(sale.cashierName || 'Operator')}</div>
            <p class="print-meta">Received ${currency.format(sale.receivedAmount)}</p>
          </div>
          <div class="print-meta-block">
            <div class="print-meta-label">FBR</div>
            <div>${this.escapeHtml(sale.fbrInvoiceNumber || sale.fbrStatus)}</div>
            <p class="print-meta">${this.escapeHtml(sale.fbrStatus)}</p>
          </div>
        </section>

        <table class="print-table">
          <thead>
            <tr>
              <th>Item</th>
              <th class="center">Qty</th>
              <th class="right">Rate</th>
              <th class="right">Total</th>
            </tr>
          </thead>
          <tbody>${lineRows}</tbody>
        </table>

        ${paymentBreakdown}

        <section class="print-totals">
          <div class="print-totals-row"><span>Subtotal</span><strong>${currency.format(sale.subtotal)}</strong></div>
          <div class="print-totals-row"><span>Discount</span><strong>- ${currency.format(sale.discount)}</strong></div>
          <div class="print-totals-row"><span>Tax</span><strong>${currency.format(sale.tax)}</strong></div>
          <div class="print-totals-row"><span>Change</span><strong>${currency.format(sale.changeAmount)}</strong></div>
          <div class="print-totals-row total"><span>Grand Total</span><strong>${currency.format(sale.total)}</strong></div>
        </section>

        <div class="print-tag-row">
          <span class="print-tag">Store: ${this.escapeHtml(brand.storeName)}</span>
          <span class="print-tag">${isSlip ? 'Thermal Slip Format' : 'A4 Invoice Format'}</span>
          <span class="print-tag">Payment: ${this.escapeHtml(sale.paymentMethod)}</span>
          <span class="print-tag">FBR: ${this.escapeHtml(sale.fbrStatus)}</span>
        </div>

        <p class="print-footnote">
          This ${isSlip ? 'receipt slip' : 'invoice'} was generated by ${this.escapeHtml(brand.platformName)} for ${this.escapeHtml(brand.storeName)} and can be reprinted later from Sales History.
        </p>
      </main>`;
  }

  private escapeHtml(value: string): string {
    return value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }
}
