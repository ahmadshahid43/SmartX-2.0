import { CommonModule, CurrencyPipe, DOCUMENT } from '@angular/common';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CartLine,
  CustomerProfile,
  CreateBookingOrderRequest,
  PosBookingOrder,
  PosCheckoutReceipt,
  PosPaymentLineRequest,
  PosProduct,
  PosSummary,
  PosTerminal,
  PosWorkflowAction,
} from '../../core/models';
import { ReceiptPrintService } from '../../core/receipt-print.service';
import { WorkspaceApiService } from '../../core/workspace-api.service';

interface TenderLineDraft {
  id: string;
  method: string;
  amount: number;
  referenceNo: string;
}

interface BookingFormDraft {
  customerName: string;
  phoneNumber: string;
  email: string;
  dueAt: string;
  notes: string;
  depositAmount: string;
  depositMethod: string;
  referenceNo: string;
}

const DEFAULT_BOOKING_FORM: BookingFormDraft = {
  customerName: '',
  phoneNumber: '',
  email: '',
  dueAt: '',
  notes: '',
  depositAmount: '',
  depositMethod: 'Cash',
  referenceNo: '',
};

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

  protected readonly terminal = signal<PosTerminal | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly selectedCategory = signal('All');
  protected readonly manualEntry = signal('');
  protected readonly manualQuantity = signal(1);
  protected readonly tenderLines = signal<TenderLineDraft[]>([]);
  protected readonly sendToFbr = signal(true);
  protected readonly autoPrintSlip = signal(false);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly paymentMessage = signal('');
  protected readonly manualMessage = signal('');
  protected readonly lastReceipt = signal<PosCheckoutReceipt | null>(null);
  protected readonly tenderOpen = signal(false);
  protected readonly bookingForm = signal<BookingFormDraft>({ ...DEFAULT_BOOKING_FORM });
  protected readonly bookingDialogOpen = signal(false);
  protected readonly activeBookingId = signal<string | null>(null);
  protected readonly bookingCollectionAmount = signal('');
  protected readonly bookingCollectionMethod = signal('Cash');
  protected readonly bookingCollectionReference = signal('');
  protected readonly bookingCollectionNotes = signal('');
  protected readonly bookingCompletionSendToFbr = signal(true);
  protected readonly customers = signal<CustomerProfile[]>([]);
  protected readonly customerPickerOpen = signal(false);
  protected readonly customerSearch = signal('');
  protected readonly newCustomerName = signal('');
  protected readonly newCustomerPhone = signal('');
  protected readonly newCustomerEmail = signal('');
  protected readonly standardTaxRatePercent = signal(17);
  protected readonly cardTaxRatePercent = signal(0);
  protected readonly taxExempt = signal(false);

  protected readonly cart = computed(() => this.terminal()?.cart ?? []);
  protected readonly cartQuantity = computed(() => this.cart().reduce((total, line) => total + line.quantity, 0));
  protected readonly heldOrders = computed(() => this.terminal()?.heldOrders ?? []);
  protected readonly bookings = computed(() => this.terminal()?.bookings ?? []);
  protected readonly paymentMethods = computed(() => this.terminal()?.paymentMethods ?? ['Cash', 'Card', 'Bank Transfer', 'Digital Wallet', 'Mixed']);
  protected readonly effectiveTaxRatePercent = computed(() => {
    if (this.taxExempt()) {
      return 0;
    }

    return this.tenderMethodLabel() === 'Card'
      ? this.cardTaxRatePercent()
      : this.standardTaxRatePercent();
  });

  protected readonly taxLabel = computed(() => {
    if (this.taxExempt()) {
      return 'Tax (exempt)';
    }

    const rate = this.effectiveTaxRatePercent();
    return this.tenderMethodLabel() === 'Card' ? `Card tax (${rate}%)` : `Tax (${rate}%)`;
  });

  protected readonly summary = computed<PosSummary>(() => {
    const base = this.terminal()?.summary ?? {
      itemCount: 0,
      subtotal: 0,
      discount: 0,
      tax: 0,
      total: 0,
    };
    const taxableAmount = Math.max(base.subtotal - base.discount, 0);
    const tax = this.taxExempt()
      ? 0
      : Math.round(taxableAmount * this.effectiveTaxRatePercent()) / 100;

    return { ...base, tax, total: taxableAmount + tax };
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

  protected readonly filteredCustomers = computed(() => {
    const query = this.customerSearch().trim().toLowerCase();
    return this.customers()
      .filter((customer) => !customer.isWalkIn)
      .filter((customer) =>
        query.length === 0 ||
        customer.name.toLowerCase().includes(query) ||
        (customer.phoneNumber ?? '').toLowerCase().includes(query) ||
        (customer.email ?? '').toLowerCase().includes(query),
      )
      .slice(0, 8);
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

  protected readonly tenderCollectedAmount = computed(() =>
    this.tenderLines().reduce((total, line) => total + (Number.isFinite(line.amount) ? line.amount : 0), 0),
  );

  protected readonly tenderMethodLabel = computed(() => {
    const methods = Array.from(
      new Set(
        this.tenderLines()
          .filter((line) => line.amount > 0)
          .map((line) => line.method),
      ),
    );

    if (methods.length === 0) {
      return this.paymentMethods()[0] ?? 'Cash';
    }

    return methods.length === 1 ? methods[0] : 'Mixed';
  });

  protected readonly changeDue = computed(() => Math.max(this.tenderCollectedAmount() - this.summary().total, 0));
  protected readonly remainingBalance = computed(() => Math.max(this.summary().total - this.tenderCollectedAmount(), 0));

  protected readonly quickCashAmounts = computed(() => {
    const total = this.summary().total;
    if (total <= 0) {
      return [];
    }

    return Array.from(
      new Set([total, this.roundUpTo(total, 100), this.roundUpTo(total, 500), this.roundUpTo(total, 1000)]),
    ).filter((amount) => amount > 0);
  });

  protected readonly selectedBooking = computed(() =>
    this.bookings().find((booking) => booking.id === this.activeBookingId()) ?? null,
  );

  protected readonly bookingCollectionRemaining = computed(() => {
    const booking = this.selectedBooking();
    if (!booking) {
      return 0;
    }

    const amount = Number(this.bookingCollectionAmount());
    const normalizedAmount = Number.isFinite(amount) ? amount : 0;
    return Math.max(booking.balanceAmount - normalizedAmount, 0);
  });

  constructor() {
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

  protected setStandardTaxRate(value: string): void {
    this.standardTaxRatePercent.set(this.normalizeTaxRate(value));
  }

  protected setCardTaxRate(value: string): void {
    this.cardTaxRatePercent.set(this.normalizeTaxRate(value));
  }

  protected setTaxExempt(value: boolean): void {
    this.taxExempt.set(value);
  }

  protected toggleCustomerPicker(): void {
    this.customerPickerOpen.update((isOpen) => !isOpen);
  }

  protected setCustomerSearch(value: string): void {
    this.customerSearch.set(value);
  }

  protected setNewCustomerField(field: 'name' | 'phone' | 'email', value: string): void {
    if (field === 'name') {
      this.newCustomerName.set(value);
      return;
    }

    if (field === 'phone') {
      this.newCustomerPhone.set(value);
      return;
    }

    this.newCustomerEmail.set(value);
  }

  protected async chooseWalkInCustomer(): Promise<void> {
    await this.selectCustomer({ customerId: null });
  }

  protected async chooseExistingCustomer(customer: CustomerProfile): Promise<void> {
    await this.selectCustomer({ customerId: customer.customerId });
  }

  protected async createAndSelectCustomer(): Promise<void> {
    const customerName = this.newCustomerName().trim();
    if (!customerName) {
      this.errorMessage.set('New customer ke liye naam zaroori hai.');
      return;
    }

    await this.selectCustomer({
      customerId: null,
      customerName,
      phoneNumber: this.newCustomerPhone().trim() || null,
      email: this.newCustomerEmail().trim() || null,
    });
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
      this.applyTerminalState(terminal, 'sync-tender');
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

  protected heldPreviewLine(orderId: string): string {
    const order = this.heldOrders().find((item) => item.id === orderId);
    if (!order) {
      return '';
    }

    return order.lines
      .slice(0, 2)
      .map((line) => `${line.name} x${line.quantity}`)
      .join(' • ');
  }

  protected heldTimestamp(value: string): string {
    return new Date(value).toLocaleTimeString('en-PK', {
      hour: 'numeric',
      minute: '2-digit',
    });
  }

  protected bookingPreview(booking: PosBookingOrder): string {
    return booking.lines
      .slice(0, 2)
      .map((line) => `${line.name} x${line.quantity}`)
      .join(' • ');
  }

  protected bookingProgress(booking: PosBookingOrder): number {
    if (booking.totalAmount <= 0) {
      return 0;
    }

    return Math.min(Math.round((booking.paidAmount / booking.totalAmount) * 100), 100);
  }

  protected openTender(): void {
    if (this.summary().itemCount === 0) {
      this.paymentMessage.set('Pehle cart me item add karein, phir payment tender khulegi.');
      return;
    }

    if (this.tenderLines().length === 0 || Math.abs(this.tenderCollectedAmount() - this.summary().total) > 0.01) {
      this.resetTenderLines(this.tenderMethodLabel(), this.summary().total);
    }

    this.tenderOpen.set(true);
  }

  protected closeTender(): void {
    this.tenderOpen.set(false);
  }

  protected chooseTenderPreset(method: string): void {
    if (method === 'Mixed') {
      const total = this.summary().total;
      this.tenderLines.set([
        this.createTenderLine('Cash', total),
        this.createTenderLine('Card', 0),
      ]);
      return;
    }

    this.resetTenderLines(method, this.summary().total);
  }

  protected addTenderLine(): void {
    const fallbackMethod = this.paymentMethods().find((method) => method !== 'Mixed') ?? 'Cash';
    this.tenderLines.update((lines) => [...lines, this.createTenderLine(fallbackMethod, 0)]);
  }

  protected removeTenderLine(lineId: string): void {
    this.tenderLines.update((lines) => {
      if (lines.length <= 1) {
        return lines;
      }

      return lines.filter((line) => line.id !== lineId);
    });
  }

  protected setTenderLineMethod(lineId: string, method: string): void {
    if (method === 'Mixed') {
      return;
    }

    this.tenderLines.update((lines) =>
      lines.map((line) => (line.id === lineId ? { ...line, method } : line)),
    );
  }

  protected setTenderLineAmount(lineId: string, value: string): void {
    const parsed = Number(value);
    const amount = Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
    this.tenderLines.update((lines) =>
      lines.map((line) => (line.id === lineId ? { ...line, amount } : line)),
    );
  }

  protected setTenderLineReference(lineId: string, value: string): void {
    this.tenderLines.update((lines) =>
      lines.map((line) => (line.id === lineId ? { ...line, referenceNo: value } : line)),
    );
  }

  protected setReceivedAmount(value: string): void {
    const lineId = this.tenderLines()[0]?.id;
    if (!lineId) {
      this.resetTenderLines(this.paymentMethods()[0] ?? 'Cash', 0);
      return;
    }

    this.setTenderLineAmount(lineId, value);
  }

  protected applyQuickCashAmount(amount: number): void {
    this.resetTenderLines('Cash', amount);
  }

  protected async holdCurrentSale(): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const action = await firstValueFrom(this.workspaceApi.holdCurrentSale({ notes: null }));
      this.applyWorkflowAction(action);
      this.tenderOpen.set(false);
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

    if (this.summary().itemCount > 0) {
      await this.holdCurrentSale();
    }

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const action = await firstValueFrom(this.workspaceApi.resumeHeldOrder(orderId));
      this.applyWorkflowAction(action);
      this.tenderOpen.set(false);
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

  protected setBookingField(field: keyof BookingFormDraft, value: string): void {
    this.bookingForm.update((form) => ({
      ...form,
      [field]: value,
    }));
  }

  protected async createBooking(): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');

    const form = this.bookingForm();
    const depositAmount = Number(form.depositAmount);
    const payments: PosPaymentLineRequest[] =
      Number.isFinite(depositAmount) && depositAmount > 0
        ? [
            {
              method: form.depositMethod || 'Cash',
              amount: depositAmount,
              referenceNo: form.referenceNo.trim() || null,
            },
          ]
        : [];
    const request: CreateBookingOrderRequest = {
      customerName: form.customerName.trim(),
      phoneNumber: form.phoneNumber.trim() || null,
      email: form.email.trim() || null,
      dueAt: form.dueAt || null,
      notes: form.notes.trim() || null,
      payments,
    };

    try {
      const action = await firstValueFrom(this.workspaceApi.createBooking(request));
      this.applyWorkflowAction(action);
      this.bookingForm.set({ ...DEFAULT_BOOKING_FORM });
      this.tenderOpen.set(false);
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Booking create nahi ho saki.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected openBookingDialog(booking: PosBookingOrder): void {
    this.activeBookingId.set(booking.id);
    this.bookingCollectionAmount.set(booking.balanceAmount > 0 ? `${booking.balanceAmount}` : '0');
    this.bookingCollectionMethod.set('Cash');
    this.bookingCollectionReference.set('');
    this.bookingCollectionNotes.set('');
    this.bookingCompletionSendToFbr.set(true);
    this.bookingDialogOpen.set(true);
  }

  protected closeBookingDialog(): void {
    this.bookingDialogOpen.set(false);
    this.activeBookingId.set(null);
  }

  protected async collectBookingPayment(): Promise<void> {
    const booking = this.selectedBooking();
    if (!booking || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const amount = Number(this.bookingCollectionAmount());
      const action = await firstValueFrom(
        this.workspaceApi.collectBookingPayment(booking.id, {
          amount,
          paymentMethod: this.bookingCollectionMethod(),
          referenceNo: this.bookingCollectionReference().trim() || null,
          notes: this.bookingCollectionNotes().trim() || null,
        }),
      );
      this.applyWorkflowAction(action);
      this.openBookingDialog(action.booking ?? booking);
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Installment collect nahi ho saki.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async completeBooking(): Promise<void> {
    const booking = this.selectedBooking();
    if (!booking || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const action = await firstValueFrom(
        this.workspaceApi.completeBooking(booking.id, { sendToFbr: this.bookingCompletionSendToFbr() }),
      );
      this.applyWorkflowAction(action);

      if (action.receipt && this.autoPrintSlip()) {
        this.receiptPrintService.printSlip(action.receipt);
      }

      this.closeBookingDialog();
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Booked order complete nahi ho saka.'));
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

    try {
      const payments = this.toPaymentRequests();
      const receipt = await firstValueFrom(
        this.workspaceApi.checkout({
          paymentMethod: this.tenderMethodLabel(),
          receivedAmount: this.tenderCollectedAmount(),
          sendToFbr: this.sendToFbr(),
          payments,
          taxRatePercent: this.effectiveTaxRatePercent(),
          taxExempt: this.taxExempt(),
        }),
      );

      this.lastReceipt.set(receipt);
      this.paymentMessage.set(
        `${receipt.referenceNo} completed for PKR ${receipt.summary.total.toLocaleString()} | Change: PKR ${receipt.changeAmount.toLocaleString()} | FBR: ${receipt.fbrStatus}${receipt.fbrInvoiceNumber ? ` (${receipt.fbrInvoiceNumber})` : ''}.`,
      );

      if (this.autoPrintSlip()) {
        this.receiptPrintService.printSlip(receipt);
      }

      this.tenderOpen.set(false);
      const terminal = await firstValueFrom(this.workspaceApi.getPosTerminal());
      this.applyTerminalState(terminal, 'reset-tender');
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

  private async loadTerminal(): Promise<void> {
    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const [terminal, customerHub] = await Promise.all([
        firstValueFrom(this.workspaceApi.getPosTerminal()),
        firstValueFrom(this.workspaceApi.getCustomerHub()),
      ]);
      this.customers.set(customerHub.customers);
      this.applyTerminalState(terminal, 'reset-tender');
    } catch {
      this.errorMessage.set('POS terminal load nahi ho saka. API run aur login session check karein.');
    } finally {
      this.busy.set(false);
    }
  }

  private async selectCustomer(request: { customerId: string | null; customerName?: string | null; phoneNumber?: string | null; email?: string | null }): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');

    try {
      const terminal = await firstValueFrom(this.workspaceApi.selectPosCustomer(request));
      this.applyTerminalState(terminal, 'sync-tender');
      this.customerPickerOpen.set(false);
      this.customerSearch.set('');
      this.newCustomerName.set('');
      this.newCustomerPhone.set('');
      this.newCustomerEmail.set('');
      const customerHub = await firstValueFrom(this.workspaceApi.getCustomerHub());
      this.customers.set(customerHub.customers);
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Customer select nahi ho saka.'));
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
      this.applyTerminalState(terminal, 'sync-tender');
    } catch (error) {
      this.errorMessage.set(this.extractError(error, 'Cart update nahi ho saka.'));
    } finally {
      this.busy.set(false);
    }
  }

  private applyWorkflowAction(action: PosWorkflowAction): void {
    this.paymentMessage.set(action.message);
    this.applyTerminalState(action.terminal, 'reset-tender');

    if (action.receipt) {
      this.lastReceipt.set(action.receipt);
    }
  }

  private applyTerminalState(terminal: PosTerminal, mode: 'reset-tender' | 'sync-tender'): void {
    this.terminal.set(terminal);

    if (terminal.summary.total <= 0) {
      this.tenderLines.set([]);
      return;
    }

    const preferredMethod = this.tenderLines()[0]?.method ?? terminal.paymentMethods[0] ?? 'Cash';
    if (mode === 'sync-tender' && this.tenderLines().length > 0) {
      this.resetTenderLines(preferredMethod, terminal.summary.total);
      return;
    }

    this.resetTenderLines(preferredMethod, terminal.summary.total);
  }

  private resetTenderLines(method: string, amount: number): void {
    this.tenderLines.set([this.createTenderLine(method === 'Mixed' ? 'Cash' : method, amount)]);
  }

  private normalizeTaxRate(value: string): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.min(Math.max(parsed, 0), 100) : 0;
  }

  private createTenderLine(method: string, amount: number): TenderLineDraft {
    const identifier =
      this.document.defaultView?.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.round(Math.random() * 1000)}`;

    return {
      id: identifier,
      method,
      amount,
      referenceNo: '',
    };
  }

  private toPaymentRequests(): PosPaymentLineRequest[] {
    return this.tenderLines()
      .filter((line) => line.amount > 0)
      .map((line) => ({
        method: line.method,
        amount: line.amount,
        referenceNo: line.referenceNo.trim() || null,
      }));
  }

  private async clearCurrentCart(successMessage: string): Promise<void> {
    const lines = [...this.cart()];
    if (lines.length === 0) {
      if (successMessage) {
        this.paymentMessage.set(successMessage);
      }
      return;
    }

    for (const line of lines) {
      const terminal = await firstValueFrom(this.workspaceApi.removeCartLine(line.productId));
      this.applyTerminalState(terminal, 'sync-tender');
    }

    this.manualEntry.set('');
    this.manualQuantity.set(1);
    this.manualMessage.set('');

    if (successMessage) {
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

  private extractError(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error && 'error' in error) {
      const apiError = error as { error?: { message?: string } };
      return apiError.error?.message ?? fallback;
    }

    return fallback;
  }
}
