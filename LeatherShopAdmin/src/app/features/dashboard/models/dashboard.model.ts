import { Order } from '../../orders/models/order.model';

export interface MonthlyRevenue {
  month: number;
  label: string;
  revenue: number;
  orderCount: number;
}

export interface OrderStatusCount {
  status: string;
  count: number;
}

export interface Dashboard {
  totalProducts: number;
  totalCustomers: number;
  totalOrders: number;
  totalRevenue: number;
  pendingOrders: number;
  lowStockProducts: number;
  revenueGrowth: number;
  orderGrowth: number;
  customerGrowth: number;
  monthlyRevenue: MonthlyRevenue[];
  ordersByStatus: OrderStatusCount[];
  recentOrders: Order[];
}
