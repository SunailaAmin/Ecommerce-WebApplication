export function formatCurrency(value) {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'USD',
  }).format(Number(value ?? 0));
}

export function formatDate(value) {
  if (!value) return '';

  return new Date(value).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** The API numbers its enums from 1, so these maps are the only place that knows. */
const ORDER_STATUS_LABELS = {
  1: 'Pending',
  2: 'Confirmed',
  3: 'Processing',
  4: 'Shipped',
  5: 'Delivered',
  6: 'Cancelled',
};

const PAYMENT_STATUS_LABELS = {
  1: 'Pending',
  2: 'Successful',
  3: 'Failed',
  4: 'Cancelled',
};

export function orderStatusLabel(status) {
  return ORDER_STATUS_LABELS[status] ?? 'Unknown';
}

export function paymentStatusLabel(status) {
  return PAYMENT_STATUS_LABELS[status] ?? 'Unknown';
}
