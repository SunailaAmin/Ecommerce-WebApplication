import { Link } from 'react-router-dom';
import { formatCurrency } from '../lib/format';

export function ProductCard({ product }) {
  return (
    <article className="card product-card">
      <div className="product-card-image">
        {product.imageUrl ? (
          <img src={product.imageUrl} alt={product.name} loading="lazy" />
        ) : (
          <span aria-hidden="true">No image</span>
        )}
      </div>
      <div className="product-card-body">
        <h3>
          <Link to={`/products/${product.id}`}>{product.name}</Link>
        </h3>
        <p className="muted">{product.categoryName}</p>
        <p className="price">{formatCurrency(product.price)}</p>
      </div>
    </article>
  );
}
