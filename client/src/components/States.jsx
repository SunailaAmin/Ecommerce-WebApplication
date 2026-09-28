export function Loading({ label = 'Loading...' }) {
  return (
    <div className="state state-loading" role="status">
      <span className="spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  );
}

export function ErrorMessage({ message, onRetry }) {
  if (!message) return null;

  return (
    <div className="state state-error" role="alert">
      <p>{message}</p>
      {onRetry && (
        <button type="button" className="button button-secondary" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}

export function EmptyState({ title, description, action }) {
  return (
    <div className="state state-empty">
      <h2>{title}</h2>
      {description && <p>{description}</p>}
      {action}
    </div>
  );
}
