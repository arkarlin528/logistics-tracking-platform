// Mirrors the API's DTOs. Kept by hand for now; could be generated from /swagger/v1/swagger.json.

export type ShipmentStatus =
  | 'Booked'
  | 'PickedUp'
  | 'InTransit'
  | 'ArrivedAtPort'
  | 'CustomsClearance'
  | 'OutForDelivery'
  | 'Delivered'
  | 'Cancelled';

export const SHIPMENT_STATUSES: ShipmentStatus[] = [
  'Booked', 'PickedUp', 'InTransit', 'ArrivedAtPort', 'CustomsClearance', 'OutForDelivery', 'Delivered', 'Cancelled',
];

export type TransportMode = 'Sea' | 'Air' | 'Road';
export type UserRole = 'Admin' | 'Dispatcher' | 'Customer';

export interface User {
  id: number;
  email: string;
  displayName: string;
  role: UserRole;
  customerId: number | null;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ShipmentSummary {
  id: number;
  reference: string;
  status: ShipmentStatus;
  mode: TransportMode;
  customerName: string;
  carrierName: string;
  originCode: string;
  destinationCode: string;
  estimatedArrival: string;
  isDelayed: boolean;
  currentLatitude: number | null;
  currentLongitude: number | null;
}

export interface Port {
  id: number;
  code: string;
  name: string;
  country: string;
  latitude: number;
  longitude: number;
}

export interface Carrier {
  id: number;
  code: string;
  name: string;
  mode: TransportMode;
}

export interface Customer {
  id: number;
  code: string;
  name: string;
  contactEmail: string;
}

export interface StatusEvent {
  id: number;
  status: ShipmentStatus;
  occurredAt: string;
  location: string | null;
  latitude: number | null;
  longitude: number | null;
  note: string | null;
}

export interface ShipmentDetail {
  id: number;
  reference: string;
  status: ShipmentStatus;
  mode: TransportMode;
  customerId: number;
  customerName: string;
  carrierName: string;
  origin: Port;
  destination: Port;
  cargoDescription: string;
  weightKg: number;
  createdAt: string;
  estimatedArrival: string;
  deliveredAt: string | null;
  isDelayed: boolean;
  currentLatitude: number | null;
  currentLongitude: number | null;
  nextStatuses: ShipmentStatus[];
  events: StatusEvent[];
}

export interface ShipmentFilter {
  search?: string;
  status?: ShipmentStatus | '';
  delayed?: boolean | null;
  page?: number;
  pageSize?: number;
}

export interface CreateShipmentRequest {
  customerId: number;
  carrierId: number;
  originPortCode: string;
  destinationPortCode: string;
  cargoDescription: string;
  weightKg: number;
  estimatedArrival: string;
}

export interface RecordEventRequest {
  status: ShipmentStatus;
  location?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  note?: string | null;
}

export interface ShipmentUpdatedMessage {
  shipmentId: number;
  reference: string;
  customerId: number;
  status: ShipmentStatus;
  isDelayed: boolean;
  estimatedArrival: string;
  currentLatitude: number | null;
  currentLongitude: number | null;
  latestEvent: StatusEvent;
}

export interface DashboardSummary {
  active: number;
  delayed: number;
  deliveredLast7Days: number;
  onTimeRateLast30Days: number;
  byStatus: { status: ShipmentStatus; count: number }[];
  mostDelayed: { id: number; reference: string; customerName: string; estimatedArrival: string; status: ShipmentStatus }[];
}

export interface FreightRate {
  id: number;
  originCode: string;
  destinationCode: string;
  carrierCode: string;
  carrierName: string;
  containerType: string;
  currency: string;
  amount: number;
  validFrom: string;
  validTo: string;
  source: string;
}

/** RFC 7807 problem response from the API. */
export interface Problem {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
