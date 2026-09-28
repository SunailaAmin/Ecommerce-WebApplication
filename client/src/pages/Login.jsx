import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { ErrorMessage } from '../components/States';

export function Login() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  const onSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setFieldErrors({});

    try {
      await signIn(email, password);
      navigate(location.state?.from ?? '/', { replace: true });
    } catch (err) {
      setError(err.message);
      setFieldErrors(err.fieldErrors ?? {});
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="auth-page">
      <div className="card auth-card">
        <h1>Sign in</h1>
        <p className="muted">Use the account you registered with.</p>

        <form className="form" onSubmit={onSubmit} noValidate>
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
              autoComplete="email"
            />
            {fieldErrors.Email && <span className="field-error">{fieldErrors.Email}</span>}
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
              autoComplete="current-password"
            />
            {fieldErrors.Password && <span className="field-error">{fieldErrors.Password}</span>}
          </label>

          {error && <ErrorMessage message={error} />}

          <button type="submit" className="button button-primary button-block" disabled={submitting}>
            {submitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>

        <p className="muted">
          No account yet? <Link to="/register">Create one</Link>
        </p>
      </div>
    </div>
  );
}
