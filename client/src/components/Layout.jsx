import { useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useCart } from '../context/CartContext';
import { useOrderNotifications } from '../lib/notifications';
import { formatDate, orderStatusLabel } from '../lib/format';

function NavBar() {
  const { user, isAuthenticated, signOut } = useAuth();
  const { totalQuantity } = useCart();

  return (
    <header className="site-header">
      <div className="container site-header-inner">
        <Link to="/" className="brand">
          Shopfront
        </Link>

        <nav className="site-nav">
          <NavLink to="/products">Products</NavLink>
          <NavLink to="/cart" className="cart-link">
            Cart{totalQuantity > 0 && <span className="cart-count">{totalQuantity}</span>}
          </NavLink>
          {isAuthenticated && <NavLink to="/orders">Orders</NavLink>}
        </nav>

        <div className="site-actions">
          {isAuthenticated ? (
            <>
              <Link to="/profile" className="muted">
                {user.firstName}
              </Link>
              <button type="button" className="button button-link" onClick={signOut}>
                Sign out
              </button>
            </>
          ) : (
            <>
              <Link to="/login">Sign in</Link>
              <Link to="/register" className="button button-primary">
                Create account
              </Link>
            </>
          )}
        </div>
      </div>
    </header>
  );
}

/** Transient banner for order events pushed over SignalR. */
function NotificationToasts({ toasts, dismiss }) {
  if (toasts.length === 0) return null;

  return (
    <div className="toasts" role="status" aria-live="polite">
      {toasts.map((toast) => (
        <div key={toast.key} className="toast">
          <div>
            <strong>{toast.orderNumber}</strong>
            <p>{toast.message}</p>
          </div>
          <button type="button" className="button button-link" onClick={() => dismiss(toast.key)}>
            Dismiss
          </button>
        </div>
      ))}
    </div>
  );
}

export function Layout() {
  const { isAuthenticated } = useAuth();
  const [toasts, setToasts] = useState([]);

  useOrderNotifications(isAuthenticated, (notification) => {
    setToasts((current) => [
      ...current,
      {
        key: `${notification.orderId}-${Date.now()}`,
        orderId: notification.orderId,
        orderNumber: notification.orderNumber,
        message: notification.message || `Order is now ${orderStatusLabel(notification.status)}.`,
        receivedAt: formatDate(new Date()),
      },
    ]);

    // Pages that show a single order refetch themselves so the status is current.
    window.dispatchEvent(new CustomEvent('order:updated', { detail: notification }));
  });

  const dismiss = (key) => setToasts((current) => current.filter((toast) => toast.key !== key));

  return (
    <div className="app">
      <NavBar />
      <NotificationToasts toasts={toasts} dismiss={dismiss} />
      <main className="container site-main">
        <Outlet />
      </main>
      <footer className="site-footer">
        <div className="container">
          <p className="muted">Shopfront &mdash; a demo store built on the ECommerce API.</p>
        </div>
      </footer>
    </div>
  );
}
