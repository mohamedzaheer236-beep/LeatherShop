/** Shared severity mapping for order/payment statuses used by PrimeNG Tag component */
export type TagSeverity = 'success' | 'secondary' | 'info' | 'warning' | 'danger' | 'contrast' | undefined;

export function getStatusSeverity(status: string | undefined): TagSeverity {
  switch (status?.toLowerCase()) {
    case 'pending':
      return 'warning';
    case 'confirmed':
      return 'info';
    case 'shipped':
      return 'info';
    case 'delivered':
      return 'success';
    case 'cancelled':
      return 'danger';
    default:
      return 'secondary';
  }
}

/** Customer category → PrimeNG Tag severity */
export function getCategorySeverity(value: string): TagSeverity {
  switch (value?.toLowerCase()) {
    case 'reseller': return 'info';
    case 'directcorporate': return 'secondary';
    case 'friendsandfamily': return 'warning';
    case 'utilityonly': return 'contrast';
    default: return 'secondary';
  }
}

/** Customer category value → display label using CUSTOMER_CATEGORIES constant */
export function getCategoryLabel(value: string, categories: { value: string; label: string }[]): string {
  return categories.find(c => c.value.toLowerCase() === value?.toLowerCase())?.label ?? value;
}
