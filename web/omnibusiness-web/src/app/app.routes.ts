import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { moduleGuard } from './core/module.guard';
import { DashboardPageComponent } from './features/dashboard/dashboard.page';
import { CustomersPageComponent } from './features/customers/customers.page';
import { FormBuilderPageComponent } from './features/form-builder/form-builder.page';
import { InventoryPageComponent } from './features/inventory/inventory.page';
import { LoginPageComponent } from './features/login/login.page';
import { OperationsPageComponent } from './features/operations/operations.page';
import { PlansPageComponent } from './features/plans/plans.page';
import { PosPageComponent } from './features/pos/pos.page';
import { ProcurementPageComponent } from './features/procurement/procurement.page';
import { SalesPageComponent } from './features/sales/sales.page';
import { ShellComponent } from './features/shell/shell.component';
import { UsersPageComponent } from './features/users/users.page';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginPageComponent,
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        component: DashboardPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'dashboard-analytics' },
      },
      {
        path: 'inventory',
        component: InventoryPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'inventory-core' },
      },
      {
        path: 'customers',
        component: CustomersPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'customer-profiles' },
      },
      {
        path: 'procurement',
        component: ProcurementPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'supplier-management' },
      },
      {
        path: 'operations',
        component: OperationsPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'fbr-compliance' },
      },
      {
        path: 'sales',
        component: SalesPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'order-listing' },
      },
      {
        path: 'pos',
        component: PosPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'pos-checkout' },
      },
      {
        path: 'users',
        component: UsersPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'employee-management' },
      },
      {
        path: 'form-builder',
        component: FormBuilderPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'no-code-builder' },
      },
      {
        path: 'plans',
        component: PlansPageComponent,
        canActivate: [moduleGuard],
        data: { module: 'plan-and-module-control' },
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
