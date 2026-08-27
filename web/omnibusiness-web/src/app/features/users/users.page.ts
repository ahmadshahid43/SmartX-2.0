import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { BranchSummary, SaveWorkspaceUserRequest, WorkspaceContext, WorkspaceStaff, WorkspaceUsers } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-users-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './users.page.html',
  styleUrl: './users.page.scss',
})
export class UsersPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly context = signal<WorkspaceContext | null>(null);
  protected readonly users = signal<WorkspaceUsers | null>(null);
  protected readonly selectedUserId = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly roles = ['Owner', 'Manager', 'Cashier', 'Back Office'];

  protected userForm: SaveWorkspaceUserRequest = this.createEmptyUser();

  protected readonly selectedUser = computed(() => {
    const items = this.users()?.items ?? [];
    return items.find((item) => item.userId === this.selectedUserId()) ?? null;
  });

  protected readonly branchOptions = computed<BranchSummary[]>(() => this.context()?.branches ?? []);

  constructor() {
    this.loadData();
  }

  protected selectUser(user: WorkspaceStaff): void {
    this.selectedUserId.set(user.userId);
    this.userForm = {
      email: user.email,
      displayName: user.displayName,
      role: user.role,
      branchId: user.branchId,
      password: null,
    };
    this.successMessage.set('');
  }

  protected startNewUser(): void {
    this.selectedUserId.set(null);
    this.userForm = this.createEmptyUser();
    this.successMessage.set('');
  }

  protected saveUser(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    const request: SaveWorkspaceUserRequest = {
      ...this.userForm,
      email: this.userForm.email.trim(),
      displayName: this.userForm.displayName.trim(),
      role: this.userForm.role,
      branchId: this.userForm.branchId,
      password: this.userForm.password?.trim() ? this.userForm.password.trim() : null,
    };

    const operation = this.selectedUserId()
      ? this.workspaceApi.updateUser(this.selectedUserId()!, request)
      : this.workspaceApi.createUser(request);

    operation.subscribe({
      next: (users) => {
        this.users.set(users);
        const currentId = this.selectedUserId();
        const savedUser = currentId
          ? users.items.find((item) => item.userId === currentId)
          : users.items.find((item) => item.email === request.email);

        if (savedUser) {
          this.selectUser(savedUser);
        } else {
          this.startNewUser();
        }

        this.loading.set(false);
        this.successMessage.set(currentId ? 'User access updated successfully.' : 'New user created successfully.');
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'User save nahi ho saka.');
        this.loading.set(false);
      },
    });
  }

  protected reload(): void {
    this.loadData();
  }

  protected roleClass(role: string): string {
    const normalized = role.toLowerCase();
    if (normalized.includes('owner') || normalized.includes('manager')) {
      return 'status-chip success';
    }

    return 'status-chip neutral';
  }

  private createEmptyUser(): SaveWorkspaceUserRequest {
    return {
      email: '',
      displayName: '',
      role: 'Cashier',
      branchId: this.context()?.branches[0]?.id ?? '',
      password: '',
    };
  }

  private loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    forkJoin({
      context: this.workspaceApi.getContext(),
      users: this.workspaceApi.getUsers(),
    }).subscribe({
      next: ({ context, users }) => {
        this.context.set(context);
        this.users.set(users);
        this.loading.set(false);

        const activeUser = this.selectedUserId()
          ? users.items.find((item) => item.userId === this.selectedUserId())
          : users.items[0];

        if (activeUser) {
          this.selectUser(activeUser);
        } else {
          this.startNewUser();
        }
      },
      error: () => {
        this.errorMessage.set('Users module load nahi ho saka. API aur login session check karein.');
        this.loading.set(false);
      },
    });
  }
}
