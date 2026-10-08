import { Routes } from '@angular/router';
import { authGuard, homeRedirect, staffGuard } from './core/guards';
import { Shell } from './layout/shell';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login').then(m => m.LoginPage), title: 'Sign in' },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', canActivate: [homeRedirect], children: [] },
      {
        path: 'dashboard',
        canActivate: [staffGuard],
        loadComponent: () => import('./pages/dashboard/dashboard').then(m => m.DashboardPage),
        title: 'Dashboard',
      },
      {
        path: 'shipments',
        loadComponent: () => import('./pages/shipments/shipments').then(m => m.ShipmentsPage),
        title: 'Shipments',
      },
      {
        path: 'shipments/new',
        canActivate: [staffGuard],
        loadComponent: () => import('./pages/new-shipment/new-shipment').then(m => m.NewShipmentPage),
        title: 'New shipment',
      },
      {
        path: 'shipments/:id',
        loadComponent: () => import('./pages/shipment-detail/shipment-detail').then(m => m.ShipmentDetailPage),
        title: 'Shipment',
      },
      {
        path: 'map',
        loadComponent: () => import('./pages/live-map/live-map').then(m => m.LiveMapPage),
        title: 'Live map',
      },
      {
        path: 'rates',
        loadComponent: () => import('./pages/rates/rates').then(m => m.RatesPage),
        title: 'Freight rates',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
