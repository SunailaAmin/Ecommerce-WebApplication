import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../lib/api';
import { ProductCard } from '../components/ProductCard';
import { ErrorMessage, Loading } from '../components/States';

export function Home() {
  const [state, setState] = useState({ products: null, categories: [], error: null, loading: true });

  useEffect(() => {
    let cancelled = false;

    Promise.all([
      api.products({ page: 1, pageSize: 8, sortBy: 'name' }),
      api.categories(),
    ])
      .then(([products, categories]) => {
        if (!cancelled) setState({ products, categories, error: null, loading: false });
      })
      .catch((err) => {
        if (!cancelled) setState({ products: null, categories: [], error: err.message, loading: false });
      });

    return () => {
      cancelled = true;
    };
  }, []);

  if (state.loading) return <Loading />;
  if (state.error) return <ErrorMessage message={state.error} />;

  const featured = state.products?.items ?? [];

  return (
    <div className="stack">
      <section className="hero">
        <h1>Everything for the desk, delivered</h1>
        <p>
          Browse the catalog, keep a cart, and check out with a test card. Nothing here charges a
          real card.
        </p>
        <Link to="/products" className="button button-primary">
          Shop all products
        </Link>
      </section>

      {state.categories.length > 0 && (
        <section>
          <h2>Categories</h2>
          <div className="chip-row">
            {state.categories.map((category) => (
              <Link key={category.id} to={`/products?categoryId=${category.id}`} className="chip">
                {category.name}
              </Link>
            ))}
          </div>
        </section>
      )}

      <section>
        <div className="section-head">
          <h2>Featured products</h2>
          <Link to="/products">View all</Link>
        </div>
        <div className="product-grid">
          {featured.map((product) => (
            <ProductCard key={product.id} product={product} />
          ))}
        </div>
      </section>
    </div>
  );
}
