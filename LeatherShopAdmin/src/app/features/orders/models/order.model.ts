export type OrderStatus = 'Pending' | 'Confirmed' | 'Shipped' | 'Delivered' | 'Cancelled';

export interface Order {
  id: number;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  totalAmount: number;
  status: OrderStatus;
  isPaid: boolean;
  createdAt: string;
  items: OrderItem[];
  /** Who cancelled this order: "Customer", "Admin", "System", or undefined for non-cancelled/legacy orders. */
  cancelledBy?: string;
  /** Courier tracking AWB number. Present when Shipped or Delivered. */
  trackingNumber?: string;
  /** Courier tracking URL. Present when Shipped or Delivered. */
  trackingLink?: string;
  /** Local UI flag – not from API */
  downloading?: boolean;
}

export interface OrderItem {
  productName: string;
  quantity: number;
  unitPrice: number;
  selectedImageUrl?: string;
}
