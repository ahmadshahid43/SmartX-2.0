import { CommonModule, CurrencyPipe, DOCUMENT } from '@angular/common';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CartLine, PosCheckoutReceipt, PosProduct, PosSummary, PosTerminal } from '../../core/models';
import { ReceiptPrintService } from '../../core/receipt-print.service';
import { WorkspaceApiService } from '../../core/workspace-api.service';

interface HeldOrderLine {
  productId: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

interface HeldOrder {
  id: string;
  label: string;
  itemCount: number;
  total: number;
  createdAt: string;
  lines: HeldOrderLine[];
}

const heldOrdersStorageKey = 'omnibusiness.held-orders';

@Component({
  selector: 'app-pos-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe],
  templateUrl: './pos.page.html',
  styleUrl: './pos.page.scss',
})
export class PosPageComponent {
  private readonly document = inject(DOCUMENT);
  private readonly workspaceApi = inject(WorkspaceApiService);
  private readonly receiptPrintService = inject(ReceiptPrintService);

  protected readonly paymentMethods = ['Cash', 'Card', 'Bank Transfer', 'Mixed'];
  protected readonly terminal = signal<PosTerminal | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly selectedCategory = signal('All');
  protected readonly manualEntry = signal('');
  protected readonly manualQuantity = signal(1);
  protected readonly selectedPaymentMethod = signal('Cash');
  protected readonly receivedAmount = signal<number | null>(null);
  protected readonly sendToFbr = signal(true);
  protected readonly autoPrintSlip = signal(false);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly paymentMessage = signal('');
  protected readonly manualMessage = signal('');
  protected readonly lastReceipt = signal<PosCheckoutReceipt | null>(null);
  protected readonly tenderOpen = signal(false);
  protected readonly heldOrders = signal<HeldOrder[]>([]);

  protected readonly cart = computed(() => this.terminal()?.cart ?? []);
  protected readonly cartQuantity = computed(() => this.cart().reduce((total, line) => total + line.quantity, 0));
  protected readonly summary = computed<PosSummary>(() => {
    return (
      this.terminal()?.summary ?? {
        itemCount: 0,
        subtotal: 0,
        discount: 0,
        tax: 0,
        total: 0,
      }
    );
  });

  protected readonly filteredProducts = computed(() => {
    const data = this.terminal();
    if (!data) {
      return [];
    }

    const normalizedSearch = this.searchTerm().trim().toLowerCase();
    const category = this.selectedCategory();

    return data.products.filter((product) => {
      const matchesCategory = category === 'All' || product.category === category;
      const matchesSearch =
        normalizedSearch.length === 0 ||
        product.name.toLowerCase().includes(normalizedSearch) ||
        product.sku.toLowerCase().includes(normalizedSearch);

      return matchesCategory && matchesSearch;
    });
  });

  protected readonly favoriteProducts = computed(() => {
    const products = this.terminal()?.products ?? [];
    return products.filter((product) => product.isFavorite).slice(0, 6);
  });

  protected readonly manualMatches = computed(() => {
    const data = this.terminal();
    const query = this.manualEntry().trim().toLowerCase();

    if (!data || query.length === 0) {
      return [];
    }

    return data.products
      .filter((product) => product.name.toLowerCase().includes(query) || product.sku.toLowerCase().includes(query))
      .slice(0, 6);
  });

  protected readonly changeDue = computed(() => {
    const received = this.receivedAmount() ?? this.summary().total;
    return Math.max(received - this.summary().total, 0);
  });

  protected readonly remainingBalance = computed(() => {
    const received = this.receivedAmount() ?? 0;
    return Math.max(this.summary().total - received, 0);
  });

  protected readonly quickCashAmounts = computed(() => {
    const total = this.summary().total;
    if (total <= 0) {
      return [];
    }

    return Array.from(
      new Set([total, this.roundUpTo(total, 100), this.roundUpTo(total, 500), this.roundUpTo(total, 1000)]),
    ).filter((amount) => amount > 0);
  });

  constructor() {
    this.loadHeldOrders();
    void this.loadTerminal();
  }

  @HostListener('window:keydown', ['$event'])
  protected handleShortcut(event: KeyboardEvent): void {
    if (event.key === 'F1') {
      event.preventDefault();
      this.focusProductSearch();
      return;
    }

    if (this.busy()) {
      return;
    }

    if (event.key === 'F6') {
      event.preventDefault();
      this.openTender();
      return;
    }

    if (event.key === 'F8') {
      event.preventDefault();
      void this.holdCurrentSale();
      return;
    }

    if (event.key === 'F10') {
      event.preventDefault();
      if (this.tenderOpen()) {
        void this.completePayment();
        return;
      }

      this.openTender();
    }
  }

  protected visualLabel(product: PosProduct): string {
    return product.visualCode.slice(0, 3);
  }

  protected setSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  protected setCategory(category: string): void {
    this.selectedCategory.set(category);
  }

  protected setManualEntry(value: string): void {
    this.manualEntry.set(value);
    this.manualMessage.set('');
  }

  protected setManualQuantity(value: string): void {
    const parsed = Number(value);
    this.manualQuantity.set(Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : 1);
  }

  protected setPaymentMethod(method: string): void {
    this.selectedPaymentMethod.set(method);
  }

  protected setReceivedAmount(value: string): void {
    const parsed = Number(value);
    this.receivedAmount.set(Number.isFinite(parsed) && parsed >= 0 ? parsed : null);
  }

  protected setSendToFbr(value: boolean): void {
    this.sendToFbr.set(value);
  }

  protected setAutoPrintSlip(value: boolean): void {
    this.autoPrintSlip.set(value);
  }

  protected addProduct(product: PosProduct): void {
    const existingLine = this.cart().find((line) => line.productId === product.productId);
    const nextQuantity = (existingLine?.quantity ?? 0) + 1;
    void this.saveCartLine(product.productId, nextQuantity);
  }

  protected addManualProduct(product?: PosProduct): void {
    const catalog = this.terminal()?.products ?? [];
    const query = this.manualEntry().trim().toLowerCase();
    const matchedProduct =
      product ??
      catalog.find((item) => item.sku.toLowerCase() === query) ??
      catalog.find((item) => item.name.toLowerCase() === query) ??
      this.manualMatches()[0];

    if (!matchedProduct) {
      this.manualMessage.set('SKU ya product name match nahi hua. Neeche suggestions se item select karein.');
      return;
    }

    const existingLine = this.cart().find((line) => line.productId === matchedProduct.productId);
    const nextQuantity = (existingLine?.quantity ?? 0) + this.manualQuantity();
    this.manualMessage.set(`${matchedProduct.name} bill me add ho gaya.`);
    this.manualEntry.set('');
    this.manualQuantity.set(1);
    void this.saveCartLine(matchedProduct.productId, nextQuantity);
  }

  protected updateQuantity(productId: string, delta: number): void {
    const existingLine = this.cart().find((line) => line.productId === productId);
    if (!existingLine) {
      return;
    }

    const nextQuantity = existingLine.quantity + delta;
    if (nextQuantity <= 0) {
      void this.removeLine(productId);
      return;
    }

    void this.saveCartLine(productId, nextQuantity);
  }

  protected async removeLine(productId: string): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const terminal = await firstValueFrom(this.workspaceApi.removeCartLine(productId));
      this.applyTerminalState(terminal, 'sync-total');
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Cart item remove nahi ho saka.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected lineTotal(line: CartLine): number {
    return line.quantity * line.unitPrice;
  }

  protected stockLabel(product: PosProduct): string {
    const reservedInCart = this.cart()
      .filter((line) => line.productId === product.productId)
      .reduce((total, line) => total + line.quantity, 0);

    return `${Math.max(product.inHand - reservedInCart, 0)} in stock`;
  }

  protected heldPreview(order: HeldOrder): string {
    return order.lines
      .slice(0, 2)
      .map((line) => `${line.name} x${line.quantity}`)
      .join(' • ');
  }

  protected heldTimestamp(order: HeldOrder): string {
    return new Date(order.createdAt).toLocaleTimeString('en-PK', {
      hour: 'numeric',
      minute: '2-digit',
    });
  }

  protected openTender(): void {
    if (this.summary().itemCount === 0) {
      this.paymentMessage.set('Pehle cart me item add karein, phir payment tender khulegi.');
      return;
    }

    this.receivedAmount.update((current) => current ?? this.summary().total);
    this.tenderOpen.set(true);
  }

  protected closeTender(): void {
    this.tenderOpen.set(false);
  }

  protected applyQuickCashAmount(amount: number): void {
    this.receivedAmount.set(amount);
  }

  protected async holdCurrentSale(silent = false): Promise<void> {
    if (this.busy()) {
      return;
    }

    if (this.summary().itemCount === 0) {
      if (!silent) {
        this.paymentMessage.set('Hold karne ke liye current cart me items hone chahiye.');
      }
      return;
    }

    const snapshot: HeldOrder = {
      id: this.document.defaultView?.crypto?.randomUUID?.() ?? `${Date.now()}`,
      label: this.upcomingTicketLabel(),
      itemCount: this.summary().itemCount,
      total: this.summary().total,
      createdAt: new Date().toISOString(),
      lines: this.cart().map((line) => ({
        productId: line.productId,
        name: line.name,
        quantity: line.quantity,
        unitPrice: line.unitPrice,
      })),
    };

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      await this.clearCurrentCart('', true);
      this.heldOrders.set([snapshot, ...this.heldOrders()].slice(0, 8));
      this.persistHeldOrders();
      this.tenderOpen.set(false);
      this.searchTerm.set('');
      this.selectedCategory.set('All');
      this.receivedAmount.set(null);

      if (!silent) {
        this.paymentMessage.set(`${snapshot.label} hold par save ho gayi. Aap next customer start kar sakte hain.`);
      }
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Sale hold par save nahi ho saki.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async resumeHeldOrder(orderId: string): Promise<void> {
    if (this.busy()) {
      return;
    }

    const order = this.heldOrders().find((heldOrder) => heldOrder.id === orderId);
    if (!order) {
      return;
    }

    if (this.summary().itemCount > 0) {
      await this.holdCurrentSale(true);
    }

    this.busy.set(true);
    this.errorMessage.set('');
    this.paymentMessage.set('');

    try {
      await this.clearCurrentCart('', true);

      for (const line of order.lines) {
        const terminal = await firstValueFrom(this.workspaceApi.saveCartLine(line.productId, line.quantity));
        this.applyTerminalState(terminal, 'sync-total');
      }

      this.heldOrders.set(this.heldOrders().filter((heldOrder) => heldOrder.id !== orderId));
      this.persistHeldOrders();
      this.receivedAmount.set(this.summary().total);
      this.tenderOpen.set(false);
      this.paymentMessage.set(`${order.label} resume ho gayi. Cashier sale continue kar sakta hai.`);
      this.focusProductSearch();
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Held sale resume nahi ho saki.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async voidCurrentSale(): Promise<void> {
    if (this.busy()) {
      return;
    }

    if (this.summary().itemCount === 0) {
      this.paymentMessage.set('Current sale already empty hai.');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      await this.clearCurrentCart('Current sale clear kar di gayi. New customer ke liye ready.');
      this.tenderOpen.set(false);
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Current sale clear nahi ho saki.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async completePayment(): Promise<void> {
    if (this.summary().itemCount === 0) {
      this.paymentMessage.set('Add at least one item to the cart before completing payment.');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');
    const receivedAmount = this.receivedAmount() ?? this.summary().total;

    try {
      const receipt = await firstValueFrom(
        this.workspaceApi.checkout({
          paymentMethod: this.selectedPaymentMethod(),
          receivedAmount,
          sendToFbr: this.sendToFbr(),
        }),
      );

      this.lastReceipt.set(receipt);
      this.paymentMessage.set(
        `${receipt.referenceNo} completed for PKR ${receipt.summary.total.toLocaleString()} | Change: PKR ${receipt.changeAmount.toLocaleString()} | FBR: ${receipt.fbrStatus}${receipt.fbrInvoiceNumber ? ` (${receipt.fbrInvoiceNumber})` : ''}.`,
      );

      if (this.autoPrintSlip()) {
        this.receiptPrintService.printSlip(receipt);
      }

      this.receivedAmount.set(null);
      this.tenderOpen.set(false);

      const terminal = await firstValueFrom(this.workspaceApi.getPosTerminal());
      this.terminal.set(terminal);
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Payment complete nahi ho saka.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected printLastInvoice(): void {
    const receipt = this.lastReceipt();
    if (!receipt) {
      this.paymentMessage.set('Pehle payment complete karein, phir invoice print hoga.');
      return;
    }

    this.receiptPrintService.printInvoice(receipt);
    this.paymentMessage.set(`Invoice ${receipt.referenceNo} print ke liye tayyar hai.`);
  }

  protected printLastSlip(): void {
    const receipt = this.lastReceipt();
    if (!receipt) {
      this.paymentMessage.set('Pehle payment complete karein, phir receipt slip print hogi.');
      return;
    }

    this.receiptPrintService.printSlip(receipt);
    this.paymentMessage.set(`Receipt slip ${receipt.referenceNo} print ke liye tayyar hai.`);
  }

  protected upcomingTicketLabel(): string {
    return `Hold ${String(this.heldOrders().length + 1).padStart(2, '0')}`;
  }

  private async loadTerminal(): Promise<void> {
    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const terminal = await firstValueFrom(this.workspaceApi.getPosTerminal());
      this.applyTerminalState(terminal, 'preserve-received');
    } catch {
      this.errorMessage.set('POS terminal load nahi ho saka. API run aur login session check karein.');
    } finally {
      this.busy.set(false);
    }
  }

  private async saveCartLine(productId: string, quantity: number): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');
    this.paymentMessage.set('');

    try {
      const terminal = await firstValueFrom(this.workspaceApi.saveCartLine(productId, quantity));
      this.applyTerminalState(terminal, 'sync-total');
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Cart update nahi ho saka.'));
    } finally {
      this.busy.set(false);
    }
  }

  private applyTerminalState(terminal: PosTerminal, mode: 'preserve-received' | 'sync-total'): void {
    this.terminal.set(terminal);

    if (mode === 'preserve-received') {
      this.receivedAmount.update((current) => current ?? (terminal.summary.total > 0 ? terminal.summary.total : null));
      return;
    }

    this.receivedAmount.set(terminal.summary.total > 0 ? terminal.summary.total : null);
  }

  private async clearCurrentCart(successMessage: string, silent = false): Promise<void> {
    const lines = [...this.cart()];
    if (lines.length === 0) {
      if (successMessage && !silent) {
        this.paymentMessage.set(successMessage);
      }
      return;
    }

    for (const line of lines) {
      const terminal = await firstValueFrom(this.workspaceApi.removeCartLine(line.productId));
      this.applyTerminalState(terminal, 'sync-total');
    }

    this.receivedAmount.set(null);
    this.manualEntry.set('');
    this.manualQuantity.set(1);
    this.manualMessage.set('');

    if (successMessage && !silent) {
      this.paymentMessage.set(successMessage);
    }
  }

  private roundUpTo(amount: number, step: number): number {
    if (amount <= 0) {
      return 0;
    }

    return Math.ceil(amount / step) * step;
  }

  private focusProductSearch(): void {
    const searchInput = this.document.querySelector<HTMLInputElement>('.pos-search-input');
    searchInput?.focus();
    searchInput?.select();
  }

  private loadHeldOrders(): void {
    try {
      const rawValue = this.document.defaultView?.localStorage.getItem(heldOrdersStorageKey);
      if (!rawValue) {
        this.heldOrders.set([]);
        return;
      }

      const parsedValue = JSON.parse(rawValue) as HeldOrder[];
      this.heldOrders.set(Array.isArray(parsedValue) ? parsedValue : []);
    } catch {
      this.heldOrders.set([]);
    }
  }

  private persistHeldOrders(): void {
    this.document.defaultView?.localStorage.setItem(heldOrdersStorageKey, JSON.stringify(this.heldOrders()));
  }

  private extractError(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error && 'error' in error) {
      const apiError = error as { error?: { message?: string } };
      return apiError.error?.message ?? fallback;
    }

    return fallback;
  }
}
