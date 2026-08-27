import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InventoryImportResult, InventoryItem, InventoryOverview, SaveProductRequest } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-inventory-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, FormsModule],
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

  protected productForm: SaveProductRequest = this.createEmptyProduct();
  protected adjustmentProductId = '';
  protected adjustmentDelta = 1;
  protected adjustmentReason = 'Manual adjustment';

  constructor() {
    this.loadInventory();
  }

  protected statusClass(item: InventoryItem): string {
    const normalized = item.status.toLowerCase();
    if (normalized.includes('low')) {
      return 'status-chip error';
    }

    if (normalized.includes('out')) {
      return 'status-chip neutral';
    }

    return 'status-chip success';
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
      isFavorite: false,
      isQuickSale: true,
      visualCode: item.visualCode,
    };
    this.adjustmentProductId = item.productId;
    this.adjustmentDelta = 1;
    this.adjustmentReason = 'Manual adjustment';
  }

  protected startNewProduct(): void {
    this.selectedProductId.set(null);
    this.productForm = this.createEmptyProduct();
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
      'SKU-1001,Sample Product,General,1250,Main Warehouse,10,0,5,Yes,Yes,PROD01',
      'SKU-1002,Second Product,Grocery,250,Main Warehouse,40,2,8,No,Yes,PROD02',
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

    const operation = this.selectedProductId()
      ? this.workspaceApi.updateProduct(this.selectedProductId()!, request)
      : this.workspaceApi.createProduct(request);

    operation.subscribe({
      next: (inventory) => {
        this.inventory.set(inventory);
        this.loading.set(false);
        this.successMessage.set(this.selectedProductId() ? 'Product updated successfully.' : 'Product created successfully.');
        if (!this.selectedProductId()) {
          this.startNewProduct();
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
          this.inventory.set(inventory);
          this.loading.set(false);
          this.successMessage.set('Stock adjustment saved successfully.');
        },
        error: (error) => {
          this.errorMessage.set(error.error?.message ?? 'Stock adjustment save nahi ho saka.');
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

  protected reload(): void {
    this.loadInventory();
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
        this.inventory.set(inventory);
        this.loading.set(false);
        this.importWarnings.set([]);
        if (inventory.items.length > 0 && !this.selectedProductId()) {
          this.selectProduct(inventory.items[0]);
          return;
        }

        if (inventory.items.length === 0) {
          this.startNewProduct();
        }
      },
      error: () => {
        this.errorMessage.set('Inventory load nahi ho saki. API run aur login session check karein.');
        this.loading.set(false);
      },
    });
  }

  private applyImportResult(result: InventoryImportResult): void {
    this.inventory.set(result.inventory);
    this.loading.set(false);
    this.importWarnings.set(result.warnings);
    this.successMessage.set(
      `${result.importedCount} row(s) processed. ${result.createdCount} created, ${result.updatedCount} updated.`,
    );

    if (result.inventory.items.length > 0) {
      this.selectProduct(result.inventory.items[0]);
      return;
    }

    this.startNewProduct();
  }
}
