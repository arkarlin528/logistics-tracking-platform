import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import {
  Carrier, CreateShipmentRequest, Customer, DashboardSummary, FreightRate, Paged, Port, Problem,
  RecordEventRequest, ShipmentDetail, ShipmentFilter, ShipmentSummary,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api`;

  shipments(filter: ShipmentFilter) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<Paged<ShipmentSummary>>(`${this.base}/shipments`, { params });
  }

  shipment(id: number) {
    return this.http.get<ShipmentDetail>(`${this.base}/shipments/${id}`);
  }

  createShipment(request: CreateShipmentRequest) {
    return this.http.post<ShipmentDetail>(`${this.base}/shipments`, request);
  }

  recordEvent(id: number, request: RecordEventRequest) {
    return this.http.post<ShipmentDetail>(`${this.base}/shipments/${id}/events`, request);
  }

  reviseEta(id: number, estimatedArrival: string, reason: string) {
    return this.http.put<ShipmentDetail>(`${this.base}/shipments/${id}/eta`, { estimatedArrival, reason });
  }

  dashboard() {
    return this.http.get<DashboardSummary>(`${this.base}/dashboard`);
  }

  ports() {
    return this.http.get<Port[]>(`${this.base}/reference/ports`);
  }

  carriers() {
    return this.http.get<Carrier[]>(`${this.base}/reference/carriers`);
  }

  customers() {
    return this.http.get<Customer[]>(`${this.base}/reference/customers`);
  }

  rates(origin: string, destination: string) {
    const params = new HttpParams().set('origin', origin).set('destination', destination);
    return this.http.get<FreightRate[]>(`${this.base}/rates`, { params });
  }
}

/** Turns an API error into one readable sentence for the UI. */
export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as Problem | null;
    if (problem?.errors) return Object.values(problem.errors).flat().join(' ');
    if (problem?.title) return problem.title;
    if (error.status === 0) return 'Cannot reach the server.';
  }
  return 'Something went wrong. Please try again.';
}
