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
