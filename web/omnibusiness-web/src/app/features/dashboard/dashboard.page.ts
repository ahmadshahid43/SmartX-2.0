import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { TransactionSummary } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPageComponent {
  protected readonly authService = inject(AuthService);
  private readonly workspaceApi = inject(WorkspaceApiService);
  protected readonly currentDateLabel = new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date());

  protected readonly dashboard = toSignal(
    this.workspaceApi.getDashboard().pipe(catchError(() => of(null))),
    { initialValue: null },
  );

  protected readonly trendPolyline = computed(() => {
    const trend = this.dashboard()?.trend ?? [];
    if (trend.length === 0) {
      return '';
    }

    const width = 780;
    const height = 260;
    const maxValue = Math.max(...trend.map((point) => point.value));
    if (maxValue <= 0) {
      return '';
    }

    const step = width / Math.max(trend.length - 1, 1);

    return trend
      .map((point, index) => {
        const x = Math.round(index * step);
        const y = Math.round(height - (point.value / maxValue) * (height - 24) - 12);
        return `${x},${y}`;
      })
      .join(' ');
  });

  protected statusClass(transaction: TransactionSummary): string {
    switch (transaction.status.toLowerCase()) {
      case 'completed':
        return 'status-chip success';
      case 'pending':
        return 'status-chip warning';
      default:
        return 'status-chip neutral';
    }
  }
}
