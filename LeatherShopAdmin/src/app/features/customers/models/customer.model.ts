export interface Customer {
  id: number;
  phoneNumber: string;
  name: string;
  address: string;
  isSubscribed: boolean;
  createdAt: string;
  orderCount: number;
  selected?: boolean;
}

export interface CreateCustomer {
  phoneNumber: string;
  name?: string;
  address?: string;
}

export interface UpdateCustomer {
  name?: string;
  address?: string;
  isSubscribed?: boolean;
}
