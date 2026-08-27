import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CustomerHub, CustomerProfile } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-customers-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './customers.page.html',
  styleUrl: './customers.page.scss',
})
export class CustomersPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly hub = signal<CustomerHub | null>(null);
  protected readonly selectedCustomerId = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly marketingReach = computed(() => this.hub()?.customers.filter((customer) => customer.marketingOptIn).length ?? 0);

  protected readonly selectedCustomer = computed(() => {
    const data = this.hub();
    if (!data) {
      return null;
    }

    return data.customers.find((customer) => customer.customerId === this.selectedCustomerId()) ?? data.customers[0] ?? null;
  });

  constructor() {
    this.loadHub();
  }

  protected selectCustomer(customer: CustomerProfile): void {
    this.selectedCustomerId.set(customer.customerId);
  }

  protected loyaltyClass(tier: string): string {
    const normalized = tier.toLowerCase();
    if (normalized.includes('platinum') || normalized.includes('gold')) {
      return 'status-chip success';
    }

    if (normalized.includes('silver')) {
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

    this.workspaceApi.getCustomerHub().subscribe({
      next: (hub) => {
        this.hub.set(hub);
        this.loading.set(false);
        this.selectedCustomerId.set(this.selectedCustomerId() ?? hub.customers[0]?.customerId ?? null);
      },
      error: () => {
        this.errorMessage.set('Customers hub load nahi ho saka. API aur login session dobara check karein.');
        this.loading.set(false);
      },
    });
  }
}
