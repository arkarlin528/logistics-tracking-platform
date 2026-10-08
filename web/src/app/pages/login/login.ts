import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { problemMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';

interface DemoAccount {
  email: string;
  role: string;
  description: string;
}

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <section class="intro">
        <h1><span class="brand-mark">◆</span> ShipTrack</h1>
        <p>Realtime shipment tracking for a regional freight forwarder: live status updates, delay alerts and freight rates.</p>
        <ul>
          <li>.NET 8 · Clean Architecture · EF Core + SQL Server</li>
          <li>SignalR live tracking, role-based access with JWT</li>
          <li>Angular · Leaflet maps</li>
        </ul>
        <p class="muted small">All companies and shipments are fake demo data.</p>
      </section>

      <form class="card" (ngSubmit)="submit()">
        <h2>Sign in</h2>
        <label>Email <input name="email" type="email" [(ngModel)]="email" autocomplete="username" required /></label>
        <label>Password <input name="password" type="password" [(ngModel)]="password" autocomplete="current-password" required /></label>
        @if (error()) {
          <div class="error">{{ error() }}</div>
        }
        <button class="btn" type="submit" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>

        <div class="demo">
          <p class="muted">Try a demo account:</p>
          @for (account of demoAccounts; track account.email) {
            <button type="button" class="btn btn-ghost demo-btn" (click)="useDemo(account)" [disabled]="busy()">
              <strong>{{ account.role }}</strong>
              <small>{{ account.description }}</small>
            </button>
          }
        </div>
      </form>
    </div>
  `,
  styles: `
    .wrap {
      min-height: 100vh; display: grid; grid-template-columns: 1.1fr 1fr; gap: 48px;
      align-items: center; max-width: 1000px; margin: 0 auto; padding: 32px 16px;
    }
    .intro h1 { font-size: 34px; margin-bottom: 12px; }
    .intro p { font-size: 16px; color: var(--muted); }
    .intro ul { padding-left: 18px; line-height: 1.9; }
    .brand-mark { color: var(--accent); }
    .small { font-size: 13px; }
    form { display: grid; gap: 14px; }
    .demo { display: grid; gap: 8px; border-top: 1px solid var(--border); padding-top: 14px; }
    .demo p { margin: 0; }
    .demo-btn { justify-content: space-between; }
    .demo-btn small { color: var(--muted); font-weight: 400; }
    @media (max-width: 760px) { .wrap { grid-template-columns: 1fr; gap: 24px; } }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly demoAccounts: DemoAccount[] = [
    { email: 'dispatcher@demo.test', role: 'Dispatcher', description: 'All shipments, can update' },
    { email: 'customer@demo.test', role: 'Customer', description: 'Own shipments only' },
    { email: 'admin@demo.test', role: 'Admin', description: 'Full access' },
  ];

  protected useDemo(account: DemoAccount): void {
    this.email = account.email;
    this.password = environment.demoPassword;
    void this.submit();
  }

  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.login(this.email, this.password);
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.error.set(problemMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
