import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../lib/api';
import { formatCurrency } from '../lib/format';
import { useAuth } from '../context/AuthContext';
import { useCart } from '../context/CartContext';
import { ErrorMessage, Loading } from '../components/States';

export function ProductDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const { addItem } = useCart();

  const [product, setProduct] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [quantity, setQuantity] = useState(1);
  const [adding, setAdding] = useState(false);
  const [message, setMessage] = useState(null);
  const [addError, setAddError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    api
      .product(id)
      .then((result) => {
        if (!cancelled) setProduct(result);
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
  }, [id]);

  const onAddToCart = async (event) => {
    event.preventDefault();

    if (!isAuthenticated) {
      navigate('/login', { state: { from: `/products/${id}` } });
      return;
    }

    setAdding(true);
    setAddError(null);
    setMessage(null);

    try {
      await addItem(product.id, quantity);
      setMessage(`Added ${quantity} x ${product.name} to your cart.`);
    } catch (err) {
      setAddError(err.message);
    } finally {
      setAdding(false);
    }
  };

  if (loading) return <Loading />;
  if (error) return <ErrorMessage message={error} />;
  if (!product) return null;

  return (
    <div className="stack">
      <Link to="/products" className="muted">
        &larr; Back to products
      </Link>

      <div className="product-detail">
        <div className="product-detail-image">
          {product.imageUrl ? (
            <img src={product.imageUrl} alt={product.name} />
          ) : (
            <span className="muted">No image available</span>
          )}
        </div>

        <div className="product-detail-body">
          <p className="muted">{product.categoryName}</p>
          <h1>{product.name}</h1>
          <p className="price price-large">{formatCurrency(product.price)}</p>
          <p className="muted">SKU: {product.sku}</p>
          {product.description && <p>{product.description}</p>}
          {!product.isActive && <p className="notice">This product is no longer available.</p>}

          <form className="add-to-cart" onSubmit={onAddToCart}>
            <label>
              Quantity
              <input
                type="number"
                min="1"
                max="99"
                value={quantity}
                onChange={(event) => setQuantity(Math.max(1, Number(event.target.value) || 1))}
              />
            </label>
            <button type="submit" className="button button-primary" disabled={adding || !product.isActive}>
              {adding ? 'Adding...' : 'Add to cart'}
            </button>
          </form>

          {message && <p className="notice notice-success">{message}</p>}
          {addError && <ErrorMessage message={addError} />}
          {!isAuthenticated && (
            <p className="muted">You will be asked to sign in before adding to the cart.</p>
          )}
        </div>
      </div>
    </div>
  );
}
