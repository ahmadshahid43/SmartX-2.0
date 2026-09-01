import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  CollectBookingPaymentRequest,
  CompleteBookingRequest,
  CreateBookingOrderRequest,
  CreateHeldOrderRequest,
  CustomerHub,
  DashboardOverview,
  ReportsHub,
  FormBuilder,
  InventoryImportResult,
  InventoryOverview,
  ModuleSettings,
  OperationsHub,
  PosCheckoutReceipt,
  PosCheckoutRequest,
  PosCartMutationRequest,
  PosTerminal,
  PosWorkflowAction,
  ProcurementHub,
  RefundSaleRequest,
  SaveStockTakeRequest,
  SaveGatePassRequest,
  SaveGoodsReceiptRequest,
  SaveStockTransferRequest,
  SelectPosCustomerRequest,
  SalesHistoryItem,
  SalesHistory,
  SaveFormFieldRequest,
  SaveModuleSettingsRequest,
  SaveProductRequest,
  SaveWorkspaceUserRequest,
  StockAdjustmentRequest,
  WarehouseHub,
  WorkspaceContext,
  WorkspaceUsers,
} from './models';

@Injectable({ providedIn: 'root' })
export class WorkspaceApiService {
  private readonly http = inject(HttpClient);

  getContext() {
    return this.http.get<WorkspaceContext>('/api/v1/foundation/context');
  }

  getModuleSettings() {
    return this.http.get<ModuleSettings>('/api/v1/foundation/module-settings');
  }

  updateModuleSettings(request: SaveModuleSettingsRequest) {
    return this.http.put<ModuleSettings>('/api/v1/foundation/module-settings', request);
  }

  getUsers() {
    return this.http.get<WorkspaceUsers>('/api/v1/users');
  }

  getCustomerHub() {
    return this.http.get<CustomerHub>('/api/v1/customers/hub');
  }

  getDashboard() {
    return this.http.get<DashboardOverview>('/api/v1/dashboard/overview');
  }

  getReportsHub() {
    return this.http.get<ReportsHub>('/api/v1/dashboard/reports');
  }

  getInventory() {
    return this.http.get<InventoryOverview>('/api/v1/inventory/overview');
  }

  importInventory(file: File) {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<InventoryImportResult>('/api/v1/inventory/imports', formData);
  }

  getPosTerminal() {
    return this.http.get<PosTerminal>('/api/v1/pos/terminal');
  }

  holdCurrentSale(request: CreateHeldOrderRequest) {
    return this.http.post<PosWorkflowAction>('/api/v1/pos/hold', request);
  }

  resumeHeldOrder(heldOrderId: string) {
    return this.http.post<PosWorkflowAction>(`/api/v1/pos/hold/${heldOrderId}/resume`, {});
  }

  createBooking(request: CreateBookingOrderRequest) {
    return this.http.post<PosWorkflowAction>('/api/v1/pos/bookings', request);
  }

  collectBookingPayment(bookingId: string, request: CollectBookingPaymentRequest) {
    return this.http.post<PosWorkflowAction>(`/api/v1/pos/bookings/${bookingId}/payments`, request);
  }

  completeBooking(bookingId: string, request: CompleteBookingRequest) {
    return this.http.post<PosWorkflowAction>(`/api/v1/pos/bookings/${bookingId}/complete`, request);
  }

  saveCartLine(productId: string, quantity: number) {
    return this.http.put<PosTerminal>(`/api/v1/pos/cart/items/${productId}`, {
      productId,
      quantity,
    } satisfies PosCartMutationRequest);
  }

  removeCartLine(productId: string) {
    return this.http.delete<PosTerminal>(`/api/v1/pos/cart/items/${productId}`);
  }

  selectPosCustomer(request: SelectPosCustomerRequest) {
    return this.http.put<PosTerminal>('/api/v1/pos/customer', request);
  }

  checkout(request: PosCheckoutRequest) {
    return this.http.post<PosCheckoutReceipt>('/api/v1/pos/checkout', request);
  }

  getSalesHistory() {
    return this.http.get<SalesHistory>('/api/v1/pos/sales');
  }

  refundSale(saleId: string, request: RefundSaleRequest) {
    return this.http.post<SalesHistoryItem>(`/api/v1/pos/sales/${saleId}/refund`, request);
  }

  getProcurementHub() {
    return this.http.get<ProcurementHub>('/api/v1/procurement/hub');
  }

  getOperationsHub() {
    return this.http.get<OperationsHub>('/api/v1/operations/hub');
  }

  getWarehouseHub() {
    return this.http.get<WarehouseHub>('/api/v1/warehouse/hub');
  }

  createStockTransfer(request: SaveStockTransferRequest) {
    return this.http.post<WarehouseHub>('/api/v1/warehouse/stock-transfers', request);
  }

  createGoodsReceipt(request: SaveGoodsReceiptRequest) {
    return this.http.post<WarehouseHub>('/api/v1/warehouse/goods-receipts', request);
  }

  createGatePass(request: SaveGatePassRequest) {
    return this.http.post<WarehouseHub>('/api/v1/warehouse/gate-passes', request);
  }

  submitSaleToFbr(saleId: string) {
    return this.http.post<SalesHistoryItem>(`/api/v1/pos/sales/${saleId}/submit-fbr`, {});
  }

  getProductCustomFields() {
    return this.http.get<FormBuilder>('/api/v1/customization/forms/product-custom-fields');
  }

  addProductCustomField(request: SaveFormFieldRequest) {
    return this.http.post<FormBuilder>('/api/v1/customization/forms/product-custom-fields/fields', request);
  }

  updateProductCustomField(fieldId: string, request: SaveFormFieldRequest) {
    return this.http.put<FormBuilder>(`/api/v1/customization/forms/product-custom-fields/fields/${fieldId}`, request);
  }

  deleteProductCustomField(fieldId: string) {
    return this.http.delete<FormBuilder>(`/api/v1/customization/forms/product-custom-fields/fields/${fieldId}`);
  }

  createProduct(request: SaveProductRequest) {
    return this.http.post<InventoryOverview>('/api/v1/inventory/products', request);
  }

  updateProduct(productId: string, request: SaveProductRequest) {
    return this.http.put<InventoryOverview>(`/api/v1/inventory/products/${productId}`, request);
  }

  adjustStock(request: StockAdjustmentRequest) {
    return this.http.post<InventoryOverview>('/api/v1/inventory/stock-adjustments', request);
  }

  createStockTake(request: SaveStockTakeRequest) {
    return this.http.post<InventoryOverview>('/api/v1/inventory/stock-takes', request);
  }

  createUser(request: SaveWorkspaceUserRequest) {
    return this.http.post<WorkspaceUsers>('/api/v1/users', request);
  }

  updateUser(userId: string, request: SaveWorkspaceUserRequest) {
    return this.http.put<WorkspaceUsers>(`/api/v1/users/${userId}`, request);
  }
}
