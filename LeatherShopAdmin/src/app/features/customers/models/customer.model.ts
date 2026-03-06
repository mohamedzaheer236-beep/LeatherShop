export interface Customer {
  id: number;
  phoneNumber: string;
  name: string;
  address: string;
  isSubscribed: boolean;
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
}

export interface UpdateCustomer {
  name?: string;
  address?: string;
  isSubscribed?: boolean;
}

export interface BulkImportResult {
  message: string;
  imported: number;
  skippedDuplicates: number;
}
