/** Shared severity mapping for order/payment statuses used by PrimeNG Tag component */
export type TagSeverity = 'success' | 'secondary' | 'info' | 'warning' | 'danger' | 'contrast' | undefined;

/** Button severity supports 'primary' in addition to tag severity values */
export type ButtonSeverity = TagSeverity | 'primary';

export function getStatusSeverity(status: string): TagSeverity {
  switch (status.toLowerCase()) {
    case 'pending':    return 'warning';
    case 'confirmed':  return 'info';
    case 'shipped':    return 'info';
    case 'delivered':  return 'success';
    case 'cancelled':  return 'danger';
    default:           return 'secondary';
  }
}

export function getStatusButtonSeverity(status: string, currentStatus: string): ButtonSeverity {
  if (status === currentStatus) return 'primary';
  return 'secondary';
}
