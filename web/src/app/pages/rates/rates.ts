import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { FreightRate, Port } from '../../core/models';

@Component({
  selector: 'app-rates',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <div class="page-head">
      <div>
        <h1>Freight rates</h1>
        <p>Valid today, cheapest first. Imported by the <strong>freight-data-pipeline</strong> project through <code>POST /api/rates/import</code>.</p>
      </div>
    </div>

    <div class="card">
      <div class="filters">
        <label>From
          <select [(ngModel)]="origin" (ngModelChange)="search()">
            @for (p of ports(); track p.code) { <option [value]="p.code">{{ p.name }} ({{ p.code }})</option> }
          </select>
        </label>
        <label>To
          <select [(ngModel)]="destination" (ngModelChange)="search()">
            @for (p of ports(); track p.code) { <option [value]="p.code">{{ p.name }} ({{ p.code }})</option> }
          </select>
        </label>
      </div>

      <div class="table-wrap">
        <table>
          <thead><tr><th>Carrier</th><th>Container</th><th class="num">Price</th><th>Valid</th><th>Source</th></tr></thead>
          <tbody>
            @for (r of rates(); track r.id; let first = $first) {
              <tr [class.best]="first">
                <td>{{ r.carrierName }} <span class="muted mono">{{ r.carrierCode }}</span></td>
                <td class="mono">{{ r.containerType }}</td>
                <td class="num"><strong>{{ r.amount | currency: r.currency : 'symbol' : '1.0-0' }}</strong></td>
                <td>{{ r.validFrom | date: 'd MMM' }} – {{ r.validTo | date: 'd MMM y' }}</td>
                <td class="muted">{{ r.source }}</td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="empty">No rates for this lane today.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: `
    .filters { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 14px; max-width: 640px; }
    .num { text-align: right; }
    tr.best td { background: #f0fdf4; }
  `,
})
export class RatesPage implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly ports = signal<Port[]>([]);
  protected readonly rates = signal<FreightRate[]>([]);
  protected origin = 'THLCH';
  protected destination = 'SGSIN';

  ngOnInit(): void {
    this.api.ports().subscribe(ports => this.ports.set(ports));
    this.search();
  }

  protected search(): void {
    this.api.rates(this.origin, this.destination).subscribe(rates => this.rates.set(rates));
  }
}
