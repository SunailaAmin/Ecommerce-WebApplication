import { orderStatusLabel, paymentStatusLabel } from '../lib/format';

export function OrderStatusBadge({ status }) {
  return <span className={`badge badge-${String(status).toLowerCase()}`}>{orderStatusLabel(status)}</span>;
}

export function PaymentStatusBadge({ status }) {
  return <span className={`badge badge-${String(status).toLowerCase()}`}>{paymentStatusLabel(status)}</span>;
}
