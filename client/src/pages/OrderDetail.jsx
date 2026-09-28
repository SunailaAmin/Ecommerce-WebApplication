import { useCallback, useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { api } from '../lib/api';
import { formatCurrency, formatDate } from '../lib/format';
import { OrderStatusBadge, PaymentStatusBadge } from '../components/StatusBadge';
import { ErrorMessage, Loading } from '../components/States';

const PENDING = 1;
const PAYMENT_SUCCEEDED = 2;
const DECLINED_SUFFIX = '0000';

export function OrderDetail() {
  const { id } = useParams();
  const location = useLocation();

  const [order, setOrder] = useState(null);
  const [payment, setPayment] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [cardNumber, setCardNumber] = useState('4111 1111 1111 1111');
  const [paying, setPaying] = useState(false);
  const [payError, setPayError] = useState(null);
  const [payNotice, setPayNotice] = useState(null);

  const load = useCallback(async () => {
    try {
      const [result, latestPayment] = await Promise.all([
        api.order(id),
        // An order that has not been paid for yet has no payment to return, and that is
        // a normal state rather than a failure, so 404 leaves the section hidden.
        api.orderPayment(id).catch((err) => (err.status === 404 ? null : Promise.reject(err))),
      ]);

      setOrder(result);
      setPayment(latestPayment);
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    setLoading(true);
    load();
  }, [load]);

  // Keep the status current when a live notification arrives for this order.
  useEffect(() => {
    const onUpdated = (event) => {
      if (String(event.detail?.orderId) === String(id)) load();
    };

    window.addEventListener('order:updated', onUpdated);
    return () => window.removeEventListener('order:updated', onUpdated);
  }, [id, load]);

  const onPay = async (event) => {
    event.preventDefault();
    setPaying(true);
    setPayError(null);
    setPayNotice(null);

    try {
      const result = await api.payOrder({
        orderId: order.id,
        cardNumber: cardNumber.replace(/\s+/g, ''),
      });

      setPayment(result);
      setPayNotice(
        result.status === PAYMENT_SUCCEEDED
          ? 'Payment approved. Your order is confirmed.'
          : 'The card was declined, so the order is still awaiting payment.',
      );
      await load();
    } catch (err) {
      setPayError(err.message);
      await load();
    } finally {
      setPaying(false);
    }
  };

  if (loading) return <Loading />;
  if (error) return <ErrorMessage message={error} />;
  if (!order) return null;

  const justPlaced = location.state?.justPlaced;
  const canRetryPayment = order.status === PENDING;

  return (
    <div className="stack">
      <Link to="/orders" className="muted">
        &larr; Back to orders
      </Link>

      <div className="page-head">
        <div>
          <h1>Order {order.orderNumber}</h1>
          <p className="muted">Placed {formatDate(order.createdAt)}</p>
        </div>
        <OrderStatusBadge status={order.status} />
      </div>

      {justPlaced && (
        <p className="notice notice-success">
          Thank you. A confirmation is on its way to {order.customerEmail}.
        </p>
      )}

      {payment && (
        <p className="muted">
          Payment of {formatCurrency(payment.amount)} <PaymentStatusBadge status={payment.status} />
          {payment.transactionId ? ` &mdash; transaction ${payment.transactionId}` : ''}
          {payment.failureReason ? ` &mdash; ${payment.failureReason}` : ''}
        </p>
      )}

      <div className="order-layout">
        <div className="card">
          <h2>Items</h2>
          <table className="table">
            <thead>
              <tr>
                <th>Product</th>
                <th className="table-right">Unit price</th>
                <th className="table-right">Quantity</th>
                <th className="table-right">Total</th>
              </tr>
            </thead>
            <tbody>
              {order.items.map((item) => (
                <tr key={item.id}>
                  <td>
                    <Link to={`/products/${item.productId}`}>{item.productName}</Link>
                    <br />
                    <span className="muted">SKU: {item.sku}</span>
                  </td>
                  <td className="table-right">{formatCurrency(item.unitPrice)}</td>
                  <td className="table-right">{item.quantity}</td>
                  <td className="table-right">{formatCurrency(item.totalPrice)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan="3" className="table-right">
                  <strong>Order total</strong>
                </td>
                <td className="table-right">
                  <strong>{formatCurrency(order.totalAmount)}</strong>
                </td>
              </tr>
            </tfoot>
          </table>
        </div>

        <aside className="stack">
          <div className="card">
            <h2>Shipping to</h2>
            <p>
              <strong>{order.shippingAddress.fullName}</strong>
              <br />
              {order.shippingAddress.addressLine}
              <br />
              {order.shippingAddress.city}
              {order.shippingAddress.state ? `, ${order.shippingAddress.state}` : ''}{' '}
              {order.shippingAddress.postalCode}
              <br />
              {order.shippingAddress.country}
            </p>
            {order.shippingAddress.phoneNumber && (
              <p className="muted">{order.shippingAddress.phoneNumber}</p>
            )}
          </div>

          {canRetryPayment && (
            <div className="card">
              <h2>Payment not completed</h2>
              <p className="muted">
                This order is waiting for payment. Any card is approved unless it ends in{' '}
                <code>{DECLINED_SUFFIX}</code>.
              </p>

              <form className="form" onSubmit={onPay}>
                <label>
                  Card number
                  <input
                    type="text"
                    inputMode="numeric"
                    value={cardNumber}
                    onChange={(event) => setCardNumber(event.target.value)}
                    required
                  />
                </label>

                {payError && <ErrorMessage message={payError} />}
                {payNotice && <p className="notice notice-success">{payNotice}</p>}

                <button type="submit" className="button button-primary button-block" disabled={paying}>
                  {paying ? 'Processing...' : `Pay ${formatCurrency(order.totalAmount)}`}
                </button>
              </form>
            </div>
          )}
        </aside>
      </div>
    </div>
  );
}
