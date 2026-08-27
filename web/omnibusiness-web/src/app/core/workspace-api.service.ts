import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  CustomerHub,
  DashboardOverview,
  FormBuilder,
  InventoryImportResult,
  InventoryOverview,
  ModuleSettings,
  OperationsHub,
  PosCheckoutReceipt,
  PosCheckoutRequest,
  PosCartMutationRequest,
  PosTerminal,
  ProcurementHub,
  SalesHistoryItem,
  SalesHistory,
  SaveFormFieldRequest,
  SaveModuleSettingsRequest,
  SaveProductRequest,
  SaveWorkspaceUserRequest,
  StockAdjustmentRequest,
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

  saveCartLine(productId: string, quantity: number) {
    return this.http.put<PosTerminal>(`/api/v1/pos/cart/items/${productId}`, {
      productId,
      quantity,
    } satisfies PosCartMutationRequest);
  }

  removeCartLine(productId: string) {
    return this.http.delete<PosTerminal>(`/api/v1/pos/cart/items/${productId}`);
  }

  checkout(request: PosCheckoutRequest) {
    return this.http.post<PosCheckoutReceipt>('/api/v1/pos/checkout', request);
  }

  getSalesHistory() {
    return this.http.get<SalesHistory>('/api/v1/pos/sales');
  }

  getProcurementHub() {
    return this.http.get<ProcurementHub>('/api/v1/procurement/hub');
  }

  getOperationsHub() {
    return this.http.get<OperationsHub>('/api/v1/operations/hub');
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

  createUser(request: SaveWorkspaceUserRequest) {
    return this.http.post<WorkspaceUsers>('/api/v1/users', request);
  }

  updateUser(userId: string, request: SaveWorkspaceUserRequest) {
    return this.http.put<WorkspaceUsers>(`/api/v1/users/${userId}`, request);
  }
}
