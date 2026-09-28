import { Link } from 'react-router-dom';
import { EmptyState } from '../components/States';

export function NotFound() {
  return (
    <EmptyState
      title="Page not found"
      description="The page you asked for does not exist."
      action={
        <Link to="/" className="button button-primary">
          Back to home
        </Link>
      }
    />
  );
}
