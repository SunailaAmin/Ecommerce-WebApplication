import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { ErrorMessage } from '../components/States';

const EMPTY = { firstName: '', lastName: '', email: '', password: '', confirmPassword: '' };

export function Register() {
  const { signUp } = useAuth();
  const navigate = useNavigate();

  const [form, setForm] = useState(EMPTY);
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  const update = (key) => (event) => setForm({ ...form, [key]: event.target.value });

  const onSubmit = async (event) => {
    event.preventDefault();

    if (form.password !== form.confirmPassword) {
      setFieldErrors({ confirmPassword: 'The two passwords do not match.' });
      return;
    }

    setSubmitting(true);
    setError(null);
    setFieldErrors({});

    try {
      await signUp({
        firstName: form.firstName,
        lastName: form.lastName,
        email: form.email,
        password: form.password,
      });
      navigate('/', { replace: true });
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
        <h1>Create account</h1>
        <p className="muted">New accounts are created as customers.</p>

        <form className="form" onSubmit={onSubmit} noValidate>
          <div className="form-row">
            <label>
              First name
              <input
                type="text"
                value={form.firstName}
                onChange={update('firstName')}
                required
                autoComplete="given-name"
              />
              {fieldErrors.FirstName && <span className="field-error">{fieldErrors.FirstName}</span>}
            </label>

            <label>
              Last name
              <input
                type="text"
                value={form.lastName}
                onChange={update('lastName')}
                required
                autoComplete="family-name"
              />
              {fieldErrors.LastName && <span className="field-error">{fieldErrors.LastName}</span>}
            </label>
          </div>

          <label>
            Email
            <input
              type="email"
              value={form.email}
              onChange={update('email')}
              required
              autoComplete="email"
            />
            {fieldErrors.Email && <span className="field-error">{fieldErrors.Email}</span>}
          </label>

          <label>
            Password
            <input
              type="password"
              value={form.password}
              onChange={update('password')}
              required
              autoComplete="new-password"
            />
            {fieldErrors.Password && <span className="field-error">{fieldErrors.Password}</span>}
          </label>

          <label>
            Confirm password
            <input
              type="password"
              value={form.confirmPassword}
              onChange={update('confirmPassword')}
              required
              autoComplete="new-password"
            />
            {fieldErrors.confirmPassword && (
              <span className="field-error">{fieldErrors.confirmPassword}</span>
            )}
          </label>

          {error && <ErrorMessage message={error} />}

          <button type="submit" className="button button-primary button-block" disabled={submitting}>
            {submitting ? 'Creating account...' : 'Create account'}
          </button>
        </form>

        <p className="muted">
          Already registered? <Link to="/login">Sign in</Link>
        </p>
      </div>
    </div>
  );
}
