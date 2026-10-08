import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginResponse, User } from './models';

const STORAGE_KEY = 'logistics.session';

interface Session {
  token: string;
  expiresAt: string;
  user: User;
}

/**
 * Holds the signed-in session in a signal. The JWT is kept in localStorage so a refresh keeps you
 * signed in; the trade-offs vs. an httpOnly cookie are in docs/adr/0004-jwt-and-role-policies.md.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<Session | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);
  readonly isStaff = computed(() => {
    const role = this.user()?.role;
    return role === 'Admin' || role === 'Dispatcher';
  });

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${environment.apiUrl}/api/auth/login`, { email, password }),
    );
    const session: Session = { token: response.accessToken, expiresAt: response.expiresAt, user: response.user };
    this.session.set(session);
    this.write(session);
  }

  logout(): void {
    this.session.set(null);
    this.write(null);
    void this.router.navigate(['/login']);
  }

  private restore(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as Session;
      return new Date(session.expiresAt) > new Date() ? session : null;
    } catch {
      return null;
    }
  }

  private write(session: Session | null): void {
    try {
      if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
      else localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage can be unavailable (private mode). The session still works for this tab.
    }
  }
}
