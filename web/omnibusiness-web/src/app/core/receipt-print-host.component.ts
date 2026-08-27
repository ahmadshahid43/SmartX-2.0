import { CommonModule } from '@angular/common';
import { Component, effect, inject, ViewEncapsulation } from '@angular/core';
import { ReceiptPrintService } from './receipt-print.service';

@Component({
  selector: 'app-receipt-print-host',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './receipt-print-host.component.html',
  styleUrl: './receipt-print-host.component.scss',
  encapsulation: ViewEncapsulation.None,
})
export class ReceiptPrintHostComponent {
  private readonly receiptPrintService = inject(ReceiptPrintService);

  protected readonly activeJob = this.receiptPrintService.activeJob;

  constructor() {
    effect(() => {
      const job = this.activeJob();
      if (!job) {
        return;
      }

      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(() => {
          if (this.activeJob()?.id !== job.id) {
            return;
          }

          this.triggerBrowserPrint(job.id, job.title);
        });
      });
    });
  }

  private triggerBrowserPrint(jobId: string, printTitle: string): void {
    const originalTitle = document.title;
    let cleaned = false;

    const cleanup = () => {
      if (cleaned) {
        return;
      }

      cleaned = true;
      window.removeEventListener('afterprint', cleanup);
      document.title = originalTitle;
      this.receiptPrintService.setPrintMode(false);

      if (this.activeJob()?.id === jobId) {
        this.receiptPrintService.clearActiveJob();
      }
    };

    document.title = printTitle;
    this.receiptPrintService.setPrintMode(true);
    window.addEventListener('afterprint', cleanup, { once: true });

    window.setTimeout(() => {
      window.print();
      window.setTimeout(cleanup, 15000);
    }, 80);
  }
}
