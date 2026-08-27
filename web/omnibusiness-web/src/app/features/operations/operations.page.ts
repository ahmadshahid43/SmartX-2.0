import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OperationsHub } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-operations-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './operations.page.html',
  styleUrl: './operations.page.scss',
})
export class OperationsPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly hub = signal<OperationsHub | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');

  constructor() {
    this.loadHub();
  }

  protected statusClass(status: string): string {
    const normalized = status.toLowerCase();
    if (normalized.includes('open') || normalized.includes('live') || normalized.includes('reported')) {
      return 'status-chip success';
    }

    if (normalized.includes('review') || normalized.includes('queued') || normalized.includes('phase')) {
      return 'status-chip warning';
    }

    if (normalized.includes('failed') || normalized.includes('rejected')) {
      return 'status-chip error';
    }

    return 'status-chip neutral';
  }

  protected varianceClass(variance: number): string {
    if (variance > 0) {
      return 'text-success mono';
    }

    if (variance < 0) {
      return 'text-danger mono';
    }

    return 'mono';
  }

  protected reload(): void {
    this.loadHub();
  }

  private loadHub(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.workspaceApi.getOperationsHub().subscribe({
      next: (hub) => {
        this.hub.set(hub);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Operations hub load nahi ho saka. API rebuild/run zaroori hai.');
        this.loading.set(false);
      },
    });
  }
}
