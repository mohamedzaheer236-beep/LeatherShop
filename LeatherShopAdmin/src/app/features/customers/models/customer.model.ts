export const CUSTOMER_CATEGORIES = [
  { label: 'Reseller', value: 'Reseller' },
  { label: 'Direct Corporate', value: 'DirectCorporate' },
  { label: 'Friends And Family', value: 'FriendsAndFamily' },
  { label: 'Utility Only', value: 'UtilityOnly' },
];

export interface Customer {
  id: number;
  phoneNumber: string;
  name: string;
  address: string;
  isSubscribed: boolean;
  category: string;
  createdAt: string;
  orderCount: number;
}

/** Customer with UI selection state - use only in component state, not for API calls */
export interface CustomerWithSelection extends Customer {
  selected?: boolean;
}

export interface CreateCustomer {
  phoneNumber: string;
  name?: string;
  address?: string;
  category: string;
}

export interface CustomerCreated {
  id: number;
  phoneNumber: string;
  name: string;
  welcomeSent: boolean;
}

export interface UpdateCustomer {
  name?: string;
  address?: string;
  isSubscribed?: boolean;
  category?: string;
}

export interface BulkImportResult {
  message: string;
  imported: number;
  skippedDuplicates: number;
}
