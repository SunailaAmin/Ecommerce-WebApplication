import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../lib/api';
import { formatDate } from '../lib/format';
import { useAuth } from '../context/AuthContext';
import { useCart } from '../context/CartContext';
import { ErrorMessage, Loading } from '../components/States';

const EMPTY = {
  fullName: '',
  addressLine: '',
  city: '',
  state: '',
  postalCode: '',
  country: '',
  phoneNumber: '',
};

export function Profile() {
  const { user } = useAuth();
  const { refresh } = useCart();

  const [addresses, setAddresses] = useState(null);
  const [error, setError] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [saved, setSaved] = useState(false);

  const loadAddresses = () =>
    api
      .addresses()
      .then(setAddresses)
      .catch((err) => setError(err.message));

  useEffect(() => {
    loadAddresses();
  }, []);

  const update = (key) => (event) => {
    setSaved(false);
    setForm({ ...form, [key]: event.target.value });
  };

  const onSubmit = async (event) => {
    event.preventDefault();
    setSaving(true);
    setSaveError(null);
    setFieldErrors({});
    setSaved(false);

    try {
      await api.createAddress({
        fullName: form.fullName,
        addressLine: form.addressLine,
        city: form.city,
        state: form.state || null,
        postalCode: form.postalCode,
        country: form.country,
        phoneNumber: form.phoneNumber || null,
      });

      setForm(EMPTY);
      setSaved(true);
      await loadAddresses();
      await refresh();
    } catch (err) {
      setSaveError(err.message);
      setFieldErrors(err.fieldErrors ?? {});
    } finally {
      setSaving(false);
    }
  };

  if (!user) return null;

  return (
    <div className="stack">
      <h1>Your profile</h1>

      <div className="card">
        <h2>Account</h2>
        <dl className="detail-list">
          <div>
            <dt>Name</dt>
            <dd>
              {user.firstName} {user.lastName}
            </dd>
          </div>
          <div>
            <dt>Email</dt>
            <dd>{user.email}</dd>
          </div>
          <div>
            <dt>Role</dt>
            <dd>{user.role}</dd>
          </div>
          <div>
            <dt>Member since</dt>
            <dd>{formatDate(user.createdAt)}</dd>
          </div>
        </dl>
      </div>

      <div className="card">
        <h2>Saved addresses</h2>

        {error && <ErrorMessage message={error} />}
        {addresses === null && !error && <Loading label="Loading addresses..." />}

        {addresses?.length === 0 && <p className="muted">You have not saved an address yet.</p>}

        {addresses?.length > 0 && (
          <ul className="address-list">
            {addresses.map((address) => (
              <li key={address.id}>
                <strong>{address.fullName}</strong>
                <br />
                {address.addressLine}, {address.city}
                {address.state ? `, ${address.state}` : ''} {address.postalCode}
                <br />
                {address.country}
                {address.phoneNumber ? ` &middot; ${address.phoneNumber}` : ''}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="card">
        <h2>Add an address</h2>

        <form className="form" onSubmit={onSubmit} noValidate>
          <div className="form-row">
            <label>
              Full name
              <input type="text" value={form.fullName} onChange={update('fullName')} required />
              {fieldErrors.FullName && <span className="field-error">{fieldErrors.FullName}</span>}
            </label>
            <label>
              Phone (optional)
              <input type="tel" value={form.phoneNumber} onChange={update('phoneNumber')} />
            </label>
          </div>

          <label>
            Address
            <input type="text" value={form.addressLine} onChange={update('addressLine')} required />
            {fieldErrors.AddressLine && <span className="field-error">{fieldErrors.AddressLine}</span>}
          </label>

          <div className="form-row">
            <label>
              City
              <input type="text" value={form.city} onChange={update('city')} required />
              {fieldErrors.City && <span className="field-error">{fieldErrors.City}</span>}
            </label>
            <label>
              State (optional)
              <input type="text" value={form.state} onChange={update('state')} />
            </label>
          </div>

          <div className="form-row">
            <label>
              Postal code
              <input type="text" value={form.postalCode} onChange={update('postalCode')} required />
              {fieldErrors.PostalCode && <span className="field-error">{fieldErrors.PostalCode}</span>}
            </label>
            <label>
              Country
              <input type="text" value={form.country} onChange={update('country')} required />
              {fieldErrors.Country && <span className="field-error">{fieldErrors.Country}</span>}
            </label>
          </div>

          {saveError && <ErrorMessage message={saveError} />}
          {saved && <p className="notice notice-success">Address saved.</p>}

          <button type="submit" className="button button-primary" disabled={saving}>
            {saving ? 'Saving...' : 'Save address'}
          </button>
        </form>
      </div>

      <p className="muted">
        Looking for your orders? <Link to="/orders">Open your order history</Link>.
      </p>
    </div>
  );
}
