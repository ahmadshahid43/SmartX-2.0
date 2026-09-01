import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';
import { canAccessModule } from './role-access';
import { WorkspaceApiService } from './workspace-api.service';

const moduleFallbackRoutes = [
  { moduleKey: 'pos-checkout', route: '/pos' },
  { moduleKey: 'order-listing', route: '/sales' },
  { moduleKey: 'customer-profiles', route: '/customers' },
  { moduleKey: 'dashboard-analytics', route: '/dashboard' },
  { moduleKey: 'supplier-management', route: '/procurement' },
  { moduleKey: 'fbr-compliance', route: '/operations' },
  { moduleKey: 'inventory-core', route: '/inventory' },
  { moduleKey: 'stock-transfer-desk', route: '/warehouse' },
  { moduleKey: 'employee-management', route: '/users' },
  { moduleKey: 'no-code-builder', route: '/form-builder' },
  { moduleKey: 'plan-and-module-control', route: '/plans' },
];

export const moduleGuard: CanActivateFn = (route) => {
  const moduleKey = route.data?.['module'] as string | undefined;
  if (!moduleKey) {
    return true;
  }

  const router = inject(Router);
  const authService = inject(AuthService);
  const workspaceApi = inject(WorkspaceApiService);

  return workspaceApi.getContext().pipe(
    map((context) => {
      const enabledModules = new Set(context.access.enabledModules);
      const currentRole = authService.currentUser()?.role ?? context.user.role;
      if (enabledModules.has(moduleKey) && canAccessModule(currentRole, moduleKey)) {
        return true;
      }

      const fallbackRoute = moduleFallbackRoutes.find((candidate) =>
        enabledModules.has(candidate.moduleKey) && canAccessModule(currentRole, candidate.moduleKey))?.route
        ?? '/login';

      return router.createUrlTree([fallbackRoute]);
    }),
    catchError(() => of(router.createUrlTree(['/login']))),
  );
};
