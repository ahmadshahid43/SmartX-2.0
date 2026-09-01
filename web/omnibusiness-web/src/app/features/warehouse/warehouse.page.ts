import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  SaveGatePassRequest,
  SaveGoodsReceiptRequest,
  SaveStockTransferRequest,
  WarehouseHub,
  WorkspaceContext,
} from '../../core/models';
import { canAccessModule } from '../../core/role-access';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-warehouse-page',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterLink],
  templateUrl: './warehouse.page.html',
  styleUrl: './warehouse.page.scss',
})
export class WarehousePageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly hub = signal<WarehouseHub | null>(null);
  protected readonly context = signal<WorkspaceContext | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');

  protected readonly transferStatuses = ['Pending Dispatch', 'In Transit', 'Needs Review'];
  protected readonly inwardStatuses = ['Received', 'Partial Received', 'Inspection Pending'];
  protected readonly gatePassStatuses = ['Prepared', 'Dispatched', 'Returned'];
  protected readonly movementTypes = ['Dispatch', 'Inter-branch', 'Return', 'Repair'];

  protected transferForm: SaveStockTransferRequest = this.createTransferForm();
  protected receiptForm: SaveGoodsReceiptRequest = this.createReceiptForm();
  protected gatePassForm: SaveGatePassRequest = this.createGatePassForm();

  protected readonly moduleState = computed(() => {
    const context = this.context();
    const enabled = new Set(context?.access.enabledModules ?? []);
    const role = context?.user.role ?? '';
    return {
      transfer: enabled.has('stock-transfer-desk') && canAccessModule(role, 'stock-transfer-desk'),
      inward: enabled.has('inward-register') && canAccessModule(role, 'inward-register'),
      gatePass: enabled.has('gate-pass-control') && canAccessModule(role, 'gate-pass-control'),
    };
  });

  protected readonly branchOptions = computed(() => this.hub()?.branches ?? []);
  protected readonly warehouseOptions = computed(() => this.hub()?.warehouses ?? []);

  constructor() {
    this.loadHub();
  }

  protected statusClass(status: string): string {
    const normalized = status.toLowerCase();
    if (normalized.includes('received') || normalized.includes('prepared') || normalized.includes('transit')) {
      return 'status-chip success';
    }

    if (normalized.includes('review') || normalized.includes('partial') || normalized.includes('pending')) {
      return 'status-chip warning';
    }

    return 'status-chip neutral';
  }

  protected reload(): void {
    this.loadHub();
  }

  protected saveTransfer(): void {
    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.createStockTransfer(this.transferForm).subscribe({
      next: (hub) => {
        this.hub.set(hub);
        this.transferForm = this.createTransferForm();
        this.saving.set(false);
        this.successMessage.set('Stock transfer create ho gaya.');
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Stock transfer save nahi ho saka.');
        this.saving.set(false);
      },
    });
  }

  protected saveReceipt(): void {
    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.createGoodsReceipt(this.receiptForm).subscribe({
      next: (hub) => {
        this.hub.set(hub);
        this.receiptForm = this.createReceiptForm();
        this.saving.set(false);
        this.successMessage.set('Inward / GRN entry save ho gayi.');
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Goods receipt save nahi ho saka.');
        this.saving.set(false);
      },
    });
  }

  protected saveGatePass(): void {
    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.createGatePass(this.gatePassForm).subscribe({
      next: (hub) => {
        this.hub.set(hub);
        this.gatePassForm = this.createGatePassForm();
        this.saving.set(false);
        this.successMessage.set('Gate pass issue ho gaya.');
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Gate pass save nahi ho saka.');
        this.saving.set(false);
      },
    });
  }

  private createTransferForm(): SaveStockTransferRequest {
    const branches = this.hub()?.branches ?? [];
    return {
      fromBranchName: branches[0] ?? 'Main Branch',
      toBranchName: branches[1] ?? '',
      units: 10,
      expectedAt: null,
      status: 'Pending Dispatch',
      notes: '',
    };
  }

  private createReceiptForm(): SaveGoodsReceiptRequest {
    const warehouses = this.hub()?.warehouses ?? [];
    return {
      purchaseOrderNo: '',
      vendorName: '',
      warehouseName: warehouses[0] ?? 'Main Warehouse',
      lineCount: 1,
      receivedUnits: 10,
      varianceUnits: 0,
      status: 'Received',
      notes: '',
    };
  }

  private createGatePassForm(): SaveGatePassRequest {
    const warehouses = this.hub()?.warehouses ?? [];
    return {
      movementType: 'Dispatch',
      warehouseName: warehouses[0] ?? 'Main Warehouse',
      destinationName: '',
      referenceNo: '',
      units: 10,
      status: 'Prepared',
      notes: '',
    };
  }

  private loadHub(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    forkJoin({
      hub: this.workspaceApi.getWarehouseHub(),
      context: this.workspaceApi.getContext(),
    }).subscribe({
      next: ({ hub, context }) => {
        this.hub.set(hub);
        this.context.set(context);
        this.transferForm = this.createTransferForm();
        this.receiptForm = this.createReceiptForm();
        this.gatePassForm = this.createGatePassForm();
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Warehouse module load nahi ho saka. API aur login session check karein.');
        this.loading.set(false);
      },
    });
  }
}
