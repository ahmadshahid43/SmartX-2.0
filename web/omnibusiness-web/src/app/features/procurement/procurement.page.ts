import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProcurementHub } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-procurement-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './procurement.page.html',
  styleUrl: './procurement.page.scss',
})
export class ProcurementPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly hub = signal<ProcurementHub | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');

  constructor() {
    this.loadHub();
  }

  protected statusClass(status: string): string {
    const normalized = status.toLowerCase();
    if (normalized.includes('received') || normalized.includes('active')) {
      return 'status-chip success';
    }

    if (normalized.includes('review') || normalized.includes('approval') || normalized.includes('partial')) {
      return 'status-chip warning';
    }

    return 'status-chip neutral';
  }

  protected reload(): void {
    this.loadHub();
  }

  private loadHub(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.workspaceApi.getProcurementHub().subscribe({
      next: (hub) => {
        this.hub.set(hub);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Procurement hub load nahi ho saka. Backend session dobara run karein.');
        this.loading.set(false);
      },
    });
  }
}
