import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, of, tap } from 'rxjs';
import { LoginRequest, LoginResponse, WorkspaceUser } from './models';

const accessTokenKey = 'omnibusiness.access-token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly accessToken = signal(localStorage.getItem(accessTokenKey));

  readonly currentUser = signal<WorkspaceUser | null>(null);
  readonly isAuthenticated = computed(() => !!this.accessToken());

  login(payload: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/v1/auth/login', payload).pipe(
      tap((response) => {
        localStorage.setItem(accessTokenKey, response.accessToken);
        this.accessToken.set(response.accessToken);
        this.currentUser.set(response.user);
      }),
    );
  }

  hydrateSession(): Observable<WorkspaceUser | null> {
    if (!this.accessToken()) {
      return of(null);
    }

    return this.http.get<WorkspaceUser>('/api/v1/auth/me').pipe(
      tap((user) => this.currentUser.set(user)),
      catchError(() => {
        this.logout(true);
        return of(null);
      }),
    );
  }

  logout(skipNavigation = false): void {
    localStorage.removeItem(accessTokenKey);
    this.accessToken.set(null);
    this.currentUser.set(null);

    if (!skipNavigation) {
      void this.router.navigateByUrl('/login');
    }
  }

  getAccessToken(): string | null {
    return this.accessToken();
  }
}
