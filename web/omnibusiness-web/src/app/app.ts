import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ReceiptPrintHostComponent } from './core/receipt-print-host.component';

@Component({
  imports: [RouterOutlet, ReceiptPrintHostComponent],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
