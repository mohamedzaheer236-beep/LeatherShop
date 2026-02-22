import { Order } from '../../orders/models/order.model';

export interface Dashboard {
  totalProducts: number;
  totalCustomers: number;
  totalOrders: number;
  totalRevenue: number;
  pendingOrders: number;
  lowStockProducts: number;
  recentOrders: Order[];
}
