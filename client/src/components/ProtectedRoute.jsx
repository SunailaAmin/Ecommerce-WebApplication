import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Loading } from './States';

/**
 * Sends signed-out visitors to the login page and remembers where they were headed,
 * so they land back there after signing in.
 */
export function ProtectedRoute({ children }) {
  const { isAuthenticated, restoring } = useAuth();
  const location = useLocation();

  if (restoring) return <Loading label="Checking your session..." />;

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  return children;
}

/** Keeps signed-in users away from the login and registration pages. */
export function GuestOnlyRoute({ children }) {
  const { isAuthenticated, restoring } = useAuth();

  if (restoring) return <Loading label="Checking your session..." />;
  if (isAuthenticated) return <Navigate to="/" replace />;

  return children;
}
