import { inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { ShipmentUpdatedMessage } from './models';

export type LiveState = 'offline' | 'connecting' | 'live' | 'reconnecting';

/**
 * One SignalR connection for the whole app. Pages subscribe to `updates` and patch their own
 * state, so a status change shows up everywhere at once without polling.
 */
@Injectable({ providedIn: 'root' })
export class TrackingService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;

  readonly state = signal<LiveState>('offline');
  readonly updates = new Subject<ShipmentUpdatedMessage>();
  /** Newest first, for the activity feed. */
  readonly recent = signal<ShipmentUpdatedMessage[]>([]);

  async start(): Promise<void> {
    if (this.connection) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/tracking`, { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    connection.on('ShipmentUpdated', (message: ShipmentUpdatedMessage) => {
      this.recent.update(list => [message, ...list].slice(0, 25));
      this.updates.next(message);
    });
    connection.onreconnecting(() => this.state.set('reconnecting'));
    connection.onreconnected(() => this.state.set('live'));
    connection.onclose(() => this.state.set('offline'));

    this.connection = connection;
    this.state.set('connecting');
    try {
      await connection.start();
      this.state.set('live');
    } catch {
      this.state.set('offline');
      this.connection = null;
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.recent.set([]);
    if (connection && connection.state !== HubConnectionState.Disconnected) await connection.stop();
    this.state.set('offline');
  }
}
