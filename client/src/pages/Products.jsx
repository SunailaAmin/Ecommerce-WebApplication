import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../lib/api';
import { ProductCard } from '../components/ProductCard';
import { EmptyState, ErrorMessage, Loading } from '../components/States';

const PAGE_SIZE = 12;
const SORT_OPTIONS = [
  { value: 'name', label: 'Name' },
  { value: 'price', label: 'Price' },
  { value: 'createdat', label: 'Newest' },
];

export function Products() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [result, setResult] = useState(null);
  const [categories, setCategories] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  const page = Number(searchParams.get('page') ?? 1);
  const search = searchParams.get('search') ?? '';
  const categoryId = searchParams.get('categoryId') ?? '';
  const sortBy = searchParams.get('sortBy') ?? 'name';
  const sortDescending = searchParams.get('sortDescending') === 'true';

  const [searchBox, setSearchBox] = useState(search);
  useEffect(() => setSearchBox(search), [search]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    api
      .categories()
      .then((result_) => {
        if (!cancelled) setCategories(result_);
      })
      .catch(() => {});

    api
      .products({
        page,
        pageSize: PAGE_SIZE,
        search,
        categoryId,
        sortBy,
        sortDescending,
      })
      .then((result_) => {
        if (!cancelled) setResult(result_);
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
  }, [page, search, categoryId, sortBy, sortDescending]);

  /** Replaces the query string, which keeps the current view linkable and back/forward friendly. */
  const updateParams = (changes) => {
    const next = new URLSearchParams(searchParams);
    for (const [key, value] of Object.entries(changes)) {
      if (value === '' || value === null || value === undefined) next.delete(key);
      else next.set(key, String(value));
    }
    setSearchParams(next);
  };

  const onSearchSubmit = (event) => {
    event.preventDefault();
    updateParams({ search: searchBox.trim(), page: 1 });
  };

  const items = result?.items ?? [];
  const totalPages = result?.totalPages ?? 0;

  return (
    <div className="stack">
      <div className="page-head">
        <h1>Products</h1>
        <form className="search-form" onSubmit={onSearchSubmit} role="search">
          <input
            type="search"
            placeholder="Search products"
            value={searchBox}
            onChange={(event) => setSearchBox(event.target.value)}
            aria-label="Search products"
          />
          <button type="submit" className="button button-secondary">
            Search
          </button>
        </form>
      </div>

      <div className="filter-bar">
        <label>
          Category
          <select
            value={categoryId}
            onChange={(event) => updateParams({ categoryId: event.target.value, page: 1 })}
          >
            <option value="">All categories</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Sort by
          <select
            value={sortBy}
            onChange={(event) => updateParams({ sortBy: event.target.value, page: 1 })}
          >
            {SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <label className="checkbox">
          <input
            type="checkbox"
            checked={sortDescending}
            onChange={(event) => updateParams({ sortDescending: event.target.checked, page: 1 })}
          />
          Descending
        </label>
      </div>

      {loading && <Loading />}
      {!loading && error && <ErrorMessage message={error} />}

      {!loading && !error && items.length === 0 && (
        <EmptyState
          title="No products found"
          description="Try a different search term or category."
          action={
            <button
              type="button"
              className="button button-secondary"
              onClick={() => setSearchParams({})}
            >
              Clear filters
            </button>
          }
        />
      )}

      {!loading && !error && items.length > 0 && (
        <>
          <p className="muted">
            {result.totalCount} product{result.totalCount === 1 ? '' : 's'} found
          </p>
          <div className="product-grid">
            {items.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>

          {totalPages > 1 && (
            <nav className="pagination" aria-label="Pagination">
              <button
                type="button"
                className="button button-secondary"
                disabled={page <= 1}
                onClick={() => updateParams({ page: page - 1 })}
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
                onClick={() => updateParams({ page: page + 1 })}
              >
                Next
              </button>
            </nav>
          )}
        </>
      )}
    </div>
  );
}
