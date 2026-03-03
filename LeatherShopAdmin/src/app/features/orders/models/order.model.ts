export interface Order {
  id: number;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  totalAmount: number;
  status: string;
  isPaid: boolean;
  createdAt: string;
  items: OrderItem[];
  /** Local UI flag – not from API */
  downloading?: boolean;
}

export interface OrderItem {
  productName: string;
  quantity: number;
  unitPrice: number;
  selectedImageUrl?: string;
}
