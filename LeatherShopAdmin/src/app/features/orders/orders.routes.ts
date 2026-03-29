import { Routes } from '@angular/router';
import { OrdersComponent } from './components/orders/orders.component';
import { OrderHistoryComponent } from './components/order-history/order-history.component';

export const ordersRoutes: Routes = [
  { path: '', component: OrdersComponent },
  { path: 'history', component: OrderHistoryComponent },
];
