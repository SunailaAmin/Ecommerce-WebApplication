import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../lib/api';
import { formatCurrency, formatDate } from '../lib/format';
import { OrderStatusBadge } from '../components/StatusBadge';
import { EmptyState, ErrorMessage, Loading } from '../components/States';

const PAGE_SIZE = 10;

export function Orders() {
  const [result, setResult] = useState(null);
  const [page, setPage] = useState(1);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    api
      .orders({ page, pageSize: PAGE_SIZE })
      .then((orders) => {
        if (!cancelled) setResult(orders);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [page]);

  if (loading) return <Loading label="Loading your orders..." />;
  if (error) return <ErrorMessage message={error} />;

  const orders = result?.items ?? [];

  if (orders.length === 0) {
    return (
      <div className="stack">
        <h1>Your orders</h1>
        <EmptyState
          title="No orders yet"
          description="Once you check out, your orders will appear here."
          action={
            <Link to="/products" className="button button-primary">
              Start shopping
            </Link>
          }
        />
      </div>
    );
  }

  const totalPages = result.totalPages ?? 0;

  return (
    <div className="stack">
      <h1>Your orders</h1>

      <div className="card">
        <table className="table">
          <thead>
            <tr>
              <th>Order</th>
              <th>Placed</th>
              <th>Status</th>
              <th className="table-right">Total</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order) => (
              <tr key={order.id}>
                <td>
                  <Link to={`/orders/${order.id}`}>{order.orderNumber}</Link>
                </td>
                <td>{formatDate(order.createdAt)}</td>
                <td>
                  <OrderStatusBadge status={order.status} />
                </td>
                <td className="table-right">{formatCurrency(order.totalAmount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <nav className="pagination" aria-label="Pagination">
          <button
            type="button"
            className="button button-secondary"
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
          >
            Previous
          </button>
          <span>
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            className="button button-secondary"
            disabled={page >= totalPages}
            onClick={() => setPage(page + 1)}
          >
            Next
          </button>
        </nav>
      )}
    </div>
  );
}
