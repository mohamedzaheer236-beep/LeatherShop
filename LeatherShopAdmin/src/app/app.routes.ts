import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadChildren: () => import('./features/dashboard/dashboard.routes').then(m => m.dashboardRoutes)
  },
  {
    path: 'products',
    loadChildren: () => import('./features/products/products.routes').then(m => m.productsRoutes)
  },
  {
    path: 'orders',
    loadChildren: () => import('./features/orders/orders.routes').then(m => m.ordersRoutes)
  },
  {
    path: 'customers',
    loadChildren: () => import('./features/customers/customers.routes').then(m => m.customersRoutes)
  },
  {
    path: 'broadcast',
    loadChildren: () => import('./features/broadcast/broadcast.routes').then(m => m.broadcastRoutes)
  },
];
