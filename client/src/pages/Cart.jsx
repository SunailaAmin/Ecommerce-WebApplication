import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useCart } from '../context/CartContext';
import { formatCurrency } from '../lib/format';
import { EmptyState, ErrorMessage, Loading } from '../components/States';

export function Cart() {
  const { cart, loading, error, updateItem, removeItem, clear } = useCart();
  const [busyItem, setBusyItem] = useState(null);
  const [actionError, setActionError] = useState(null);

  const runAction = async (itemId, action) => {
    setBusyItem(itemId);
    setActionError(null);
    try {
      await action();
    } catch (err) {
      setActionError(err.message);
    } finally {
      setBusyItem(null);
    }
  };

  if (loading && !cart) return <Loading label="Loading your cart..." />;
  if (error && !cart) return <ErrorMessage message={error} />;

  const items = cart?.items ?? [];

  if (items.length === 0) {
    return (
      <div className="stack">
        <h1>Your cart</h1>
        <EmptyState
          title="Your cart is empty"
          description="Add something from the catalog to get started."
          action={
            <Link to="/products" className="button button-primary">
              Browse products
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="stack">
      <h1>Your cart</h1>

      {actionError && <ErrorMessage message={actionError} />}

      <div className="cart-layout">
        <div className="card">
          <ul className="cart-items">
            {items.map((item) => (
              <li key={item.id} className="cart-item">
                <div className="cart-item-image">
                  {item.imageUrl ? (
                    <img src={item.imageUrl} alt={item.productName} />
                  ) : (
                    <span className="muted" aria-hidden="true">
                      {item.productName.slice(0, 1)}
                    </span>
                  )}
                </div>

                <div className="cart-item-info">
                  <h3>
                    <Link to={`/products/${item.productId}`}>{item.productName}</Link>
                  </h3>
                  <p className="muted">SKU: {item.sku}</p>
                  <p className="muted">{formatCurrency(item.unitPrice)} each</p>
                </div>

                <label className="cart-item-quantity">
                  Quantity
                  <input
                    type="number"
                    min="1"
                    max={Math.max(item.availableStock, 1)}
                    value={item.quantity}
                    disabled={busyItem === item.id}
                    onChange={(event) =>
                      runAction(item.id, () => updateItem(item.id, Number(event.target.value) || 1))
                    }
                  />
                  {item.availableStock < item.quantity && (
                    <span className="field-error">Only {item.availableStock} in stock</span>
                  )}
                </label>

                <p className="cart-item-total">{formatCurrency(item.lineTotal)}</p>

                <button
                  type="button"
                  className="button button-link"
                  disabled={busyItem === item.id}
                  onClick={() => runAction(item.id, () => removeItem(item.id))}
                >
                  Remove
                </button>
              </li>
            ))}
          </ul>
        </div>

        <aside className="card cart-summary">
          <h2>Summary</h2>
          <dl>
            <div>
              <dt>Items</dt>
              <dd>{cart.totalQuantity}</dd>
            </div>
            <div className="cart-summary-total">
              <dt>Subtotal</dt>
              <dd>{formatCurrency(cart.subtotal)}</dd>
            </div>
          </dl>
          <p className="muted">Shipping and taxes are not charged in this demo.</p>
          <Link to="/checkout" className="button button-primary button-block">
            Proceed to checkout
          </Link>
          <button
            type="button"
            className="button button-secondary button-block"
            onClick={() => runAction(null, clear)}
            disabled={busyItem === null && loading}
          >
            Clear cart
          </button>
        </aside>
      </div>
    </div>
  );
}
