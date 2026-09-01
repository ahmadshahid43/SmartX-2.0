import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InventoryImportResult, InventoryItem, InventoryOverview, InventoryUsageInsight, SaveProductRequest } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-inventory-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe, FormsModule],
  templateUrl: './inventory.page.html',
  styleUrl: './inventory.page.scss',
})
export class InventoryPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly inventory = signal<InventoryOverview | null>(null);
  protected readonly selectedProductId = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly importWarnings = signal<string[]>([]);
  protected readonly searchTerm = signal('');
  protected readonly selectedWarehouseFilter = signal('all');
  protected readonly selectedCategoryFilter = signal('all');

  protected readonly selectedProduct = computed(() =>
    this.inventory()?.items.find((item) => item.productId === this.selectedProductId()) ?? null,
  );

  protected readonly filteredItems = computed(() => {
    const data = this.inventory();
    if (!data) {
      return [];
    }

    const query = this.searchTerm().trim().toLowerCase();
    const warehouse = this.selectedWarehouseFilter();
    const category = this.selectedCategoryFilter();

    return data.items.filter((item) => {
      const matchesWarehouse = warehouse === 'all' || item.warehouse === warehouse;
      const matchesCategory = category === 'all' || item.category === category;
      const matchesQuery =
        query.length === 0 ||
        item.productName.toLowerCase().includes(query) ||
        item.sku.toLowerCase().includes(query) ||
        item.visualCode.toLowerCase().includes(query);

      return matchesWarehouse && matchesCategory && matchesQuery;
    });
  });

  protected readonly inventorySummary = computed(() => {
    const inventory = this.inventory();
    const items = inventory?.items ?? [];
    const fallbackLowStockCount = items.filter((item) => item.available <= item.reorderLevel).length;
    const fallbackTotalValue = items.reduce((sum, item) => sum + item.value, 0);
    const quickReadyCount = items.filter((item) => item.available > 0).length;
    const metrics = inventory?.metrics;

    return {
      totalProducts: metrics?.totalProducts ?? items.length,
      lowStockCount: metrics?.lowStockCount ?? fallbackLowStockCount,
      totalValue: metrics?.totalValue ?? fallbackTotalValue,
      quickReadyCount,
      warehouseCount: metrics?.warehouseCount ?? inventory?.warehouses.length ?? 0,
      categoryCount: metrics?.categoryCount ?? inventory?.categories.length ?? 0,
      stockTakeCount30Days: metrics?.stockTakeCount30Days ?? 0,
      turnoverRatio30Days: metrics?.turnoverRatio30Days ?? 0,
    };
  });

  protected productForm: SaveProductRequest = this.createEmptyProduct();
  protected adjustmentProductId = '';
  protected adjustmentDelta = 1;
  protected adjustmentReason = 'Manual adjustment';
  protected stockTakeProductId = '';
  protected stockTakeCountedQuantity = 0;
  protected stockTakeNotes = '';

  constructor() {
    this.loadInventory();
  }

  protected statusClass(item: InventoryItem): string {
    return this.labelStatusClass(item.status);
  }

  protected labelStatusClass(status: string): string {
    const normalized = status.toLowerCase();

    if (normalized.includes('loss') || normalized.includes('low') || normalized.includes('out')) {
      return 'status-chip error';
    }

    if (normalized.includes('pending') || normalized.includes('tight') || normalized.includes('review')) {
      return 'status-chip warning';
    }

    return 'status-chip success';
  }

  protected coverageClass(label: string): string {
    const normalized = label.toLowerCase();

    if (normalized.includes('urgent') || normalized.includes('out')) {
      return 'status-chip error';
    }

    if (normalized.includes('tight')) {
      return 'status-chip warning';
    }

    if (normalized.includes('deep')) {
      return 'status-chip neutral';
    }

    return 'status-chip success';
  }

  protected varianceClass(value: number): string {
    if (value < 0) {
      return 'text-danger';
    }

    if (value > 0) {
      return 'text-success';
    }

    return 'text-muted';
  }

  protected selectProduct(item: InventoryItem): void {
    this.selectedProductId.set(item.productId);
    this.productForm = {
      sku: item.sku,
      name: item.productName,
      category: item.category,
      unitPrice: item.unitPrice,
      warehouse: item.warehouse,
      inHand: item.inHand,
      reserved: item.reserved,
      reorderLevel: item.reorderLevel,
      isFavorite: item.isFavorite,
      isQuickSale: item.isQuickSale,
      visualCode: item.visualCode,
    };
    this.adjustmentProductId = item.productId;
    this.adjustmentDelta = 1;
    this.adjustmentReason = 'Manual adjustment';
    this.stockTakeProductId = item.productId;
    this.stockTakeCountedQuantity = item.inHand;
    this.stockTakeNotes = `Cycle count for ${item.productName}`;
  }

  protected startNewProduct(): void {
    this.selectedProductId.set(null);
    this.productForm = this.createEmptyProduct();
  }

  protected clearFilters(): void {
    this.searchTerm.set('');
    this.selectedWarehouseFilter.set('all');
    this.selectedCategoryFilter.set('all');
  }

  protected handleInventoryImport(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0];
    if (!file) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.importWarnings.set([]);

    this.workspaceApi.importInventory(file).subscribe({
      next: (result) => {
        this.applyImportResult(result);
        if (input) {
          input.value = '';
        }
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Inventory import nahi ho saka.');
        this.loading.set(false);
        if (input) {
          input.value = '';
        }
      },
    });
  }

  protected downloadTemplate(): void {
    const csvTemplate = [
      'SKU,Name,Category,Unit Price,Warehouse,In Hand,Reserved,Reorder Level,Is Favorite,Is Quick Sale,Visual Code',
      'MED-1001,Paracetamol 500mg,Pharmacy,145,Main Warehouse,120,8,35,Yes,Yes,PAR500',
      'MED-1002,Augmentin 625mg,Pharmacy,585,Main Warehouse,42,3,18,No,Yes,AUG625',
    ].join('\n');

    const blob = new Blob([csvTemplate], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'smartx-inventory-template.csv';
    link.click();
    URL.revokeObjectURL(url);
  }

  protected saveProduct(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.importWarnings.set([]);

    const request = {
      ...this.productForm,
      unitPrice: Number(this.productForm.unitPrice),
      inHand: Number(this.productForm.inHand),
      reserved: Number(this.productForm.reserved),
      reorderLevel: Number(this.productForm.reorderLevel),
    };

    const selectedProductId = this.selectedProductId();
    const operation = selectedProductId
      ? this.workspaceApi.updateProduct(selectedProductId, request)
      : this.workspaceApi.createProduct(request);

    operation.subscribe({
      next: (inventory) => {
        this.applyInventoryOverview(inventory, selectedProductId);
        this.loading.set(false);
        this.successMessage.set(selectedProductId ? 'Product updated successfully.' : 'Product created successfully.');
        if (!selectedProductId) {
          this.productForm = this.createEmptyProduct();
        }
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Product save nahi ho saka.');
        this.loading.set(false);
      },
    });
  }

  protected applyAdjustment(): void {
    if (!this.adjustmentProductId) {
      this.errorMessage.set('Stock adjust karne ke liye pehle product select karein.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.importWarnings.set([]);

    this.workspaceApi
      .adjustStock({
        productId: this.adjustmentProductId,
        quantityDelta: Number(this.adjustmentDelta),
        reason: this.adjustmentReason,
      })
      .subscribe({
        next: (inventory) => {
          this.applyInventoryOverview(inventory, this.adjustmentProductId);
          this.loading.set(false);
          this.successMessage.set('Stock adjustment saved successfully.');
        },
        error: (error) => {
          this.errorMessage.set(error.error?.message ?? 'Stock adjustment save nahi ho saka.');
          this.loading.set(false);
        },
      });
  }

  protected applyStockTake(): void {
    if (!this.stockTakeProductId) {
      this.errorMessage.set('Stock take ke liye pehle product select karein.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.importWarnings.set([]);

    this.workspaceApi
      .createStockTake({
        productId: this.stockTakeProductId,
        countedQuantity: Number(this.stockTakeCountedQuantity),
        notes: this.stockTakeNotes.trim() || null,
      })
      .subscribe({
        next: (inventory) => {
          this.applyInventoryOverview(inventory, this.stockTakeProductId);
          this.loading.set(false);
          this.successMessage.set('Stock take posted successfully.');
        },
        error: (error) => {
          this.errorMessage.set(error.error?.message ?? 'Stock take save nahi ho saka.');
          this.loading.set(false);
        },
      });
  }

  protected quickAdjust(item: InventoryItem, delta: number): void {
    this.adjustmentProductId = item.productId;
    this.adjustmentDelta = delta;
    this.adjustmentReason = delta > 0 ? 'Quick stock addition' : 'Quick shrinkage adjustment';
    this.applyAdjustment();
  }

  protected loadIntoStockTake(item: InventoryItem): void {
    this.selectProduct(item);
  }

  protected loadLowStockItem(productId: string): void {
    const item = this.inventory()?.items.find((candidate) => candidate.productId === productId);
    if (item) {
      this.selectProduct(item);
    }
  }

  protected reload(): void {
    this.loadInventory();
  }

  protected usageTrendLabel(item: InventoryUsageInsight): string {
    if (item.soldUnits30Days > 0) {
      return `${item.soldUnits30Days} sold / 30d`;
    }

    if (item.netAdjustment30Days !== 0) {
      return `${item.netAdjustment30Days > 0 ? '+' : ''}${item.netAdjustment30Days} adj / 30d`;
    }

    return 'No movement';
  }

  private createEmptyProduct(): SaveProductRequest {
    return {
      sku: '',
      name: '',
      category: 'Retail',
      unitPrice: 0,
      warehouse: 'Main Warehouse',
      inHand: 0,
      reserved: 0,
      reorderLevel: 5,
      isFavorite: false,
      isQuickSale: true,
      visualCode: '',
    };
  }

  private loadInventory(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.workspaceApi.getInventory().subscribe({
      next: (inventory) => {
        this.applyInventoryOverview(inventory, this.selectedProductId());
        this.loading.set(false);
        this.importWarnings.set([]);
      },
      error: () => {
        this.errorMessage.set('Inventory load nahi ho saki. API run aur login session check karein.');
        this.loading.set(false);
      },
    });
  }

  private applyImportResult(result: InventoryImportResult): void {
    this.applyInventoryOverview(result.inventory);
    this.loading.set(false);
    this.importWarnings.set(result.warnings);
    this.successMessage.set(
      `${result.importedCount} row(s) processed. ${result.createdCount} created, ${result.updatedCount} updated.`,
    );
  }

  private applyInventoryOverview(overview: InventoryOverview, preferredProductId?: string | null): void {
    this.inventory.set(overview);
    const targetId = preferredProductId ?? this.selectedProductId();
    const selected = targetId
      ? overview.items.find((item) => item.productId === targetId) ?? null
      : overview.items[0] ?? null;

    if (selected) {
      this.selectProduct(selected);
      return;
    }

    this.selectedProductId.set(null);
    this.productForm = this.createEmptyProduct();
    this.adjustmentProductId = '';
    this.stockTakeProductId = '';
    this.stockTakeCountedQuantity = 0;
    this.stockTakeNotes = '';
  }
}
