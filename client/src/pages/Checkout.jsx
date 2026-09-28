import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import { useCart } from '../context/CartContext';
import { formatCurrency, orderStatusLabel } from '../lib/format';
import { EmptyState, ErrorMessage, Loading } from '../components/States';

const EMPTY_ADDRESS = {
  fullName: '',
  addressLine: '',
  city: '',
  state: '',
  postalCode: '',
  country: '',
  phoneNumber: '',
};

const PENDING = 1;

export function Checkout() {
  const navigate = useNavigate();
  const { cart, refresh } = useCart();

  const [addresses, setAddresses] = useState([]);
  const [selectedAddressId, setSelectedAddressId] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [addressForm, setAddressForm] = useState(EMPTY_ADDRESS);
  const [addressError, setAddressError] = useState(null);
  const [addressFieldErrors, setAddressFieldErrors] = useState({});
  const [savingAddress, setSavingAddress] = useState(false);

  const [cardNumber, setCardNumber] = useState('4111 1111 1111 1111');
  const [cardHolderName, setCardHolderName] = useState('');
  const [checkoutError, setCheckoutError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    api
      .addresses()
      .then((result) => {
        if (cancelled) return;
        setAddresses(result);
        if (result.length > 0) setSelectedAddressId(String(result[0].id));
        setShowForm(result.length === 0);
      })
      .catch((err) => {
        if (!cancelled) setAddressError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const updateAddressField = (key) => (event) =>
    setAddressForm({ ...addressForm, [key]: event.target.value });

  const onSaveAddress = async (event) => {
    event.preventDefault();
    setSavingAddress(true);
    setAddressError(null);
    setAddressFieldErrors({});

    try {
      const created = await api.createAddress({
        fullName: addressForm.fullName,
        addressLine: addressForm.addressLine,
        city: addressForm.city,
        state: addressForm.state || null,
        postalCode: addressForm.postalCode,
        country: addressForm.country,
        phoneNumber: addressForm.phoneNumber || null,
      });

      setAddresses((current) => [...current, created]);
      setSelectedAddressId(String(created.id));
      setAddressForm(EMPTY_ADDRESS);
      setShowForm(false);
    } catch (err) {
      setAddressError(err.message);
      setAddressFieldErrors(err.fieldErrors ?? {});
    } finally {
      setSavingAddress(false);
    }
  };

  const onSubmitPayment = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setCheckoutError(null);

    try {
      // The server prices the order, so nothing about the total is trusted from here.
      const result = await api.checkout({
        shippingAddressId: Number(selectedAddressId),
        cardNumber: cardNumber.replace(/\s+/g, ''),
        cardHolderName: cardHolderName || null,
      });

      await refresh();
      navigate(`/orders/${result.order.id}`, { state: { justPlaced: true } });
    } catch (err) {
      setCheckoutError(err.message);
      await refresh();
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <Loading />;

  if (!cart || cart.items.length === 0) {
    return (
      <div className="stack">
        <h1>Checkout</h1>
        <EmptyState
          title="There is nothing to check out"
          description="Add a product to your cart first."
          action={
            <Link to="/products" className="button button-primary">
              Browse products
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="stack">
      <h1>Checkout</h1>

      <div className="checkout-layout">
        <div className="stack">
          <section className="card">
            <div className="section-head">
              <h2>Shipping address</h2>
              {addresses.length > 0 && (
                <button
                  type="button"
                  className="button button-link"
                  onClick={() => setShowForm((current) => !current)}
                >
                  {showForm ? 'Cancel' : 'Use a new address'}
                </button>
              )}
            </div>

            {addressError && <ErrorMessage message={addressError} />}

            {addresses.length > 0 && (
              <div className="radio-list">
                {addresses.map((address) => (
                  <label key={address.id} className="radio">
                    <input
                      type="radio"
                      name="address"
                      value={address.id}
                      checked={selectedAddressId === String(address.id)}
                      onChange={(event) => setSelectedAddressId(event.target.value)}
                    />
                    <span>
                      <strong>{address.fullName}</strong>
                      <br />
                      {address.addressLine}, {address.city}
                      {address.state ? `, ${address.state}` : ''} {address.postalCode},{' '}
                      {address.country}
                    </span>
                  </label>
                ))}
              </div>
            )}

            {(showForm || addresses.length === 0) && (
              <form className="form" onSubmit={onSaveAddress} noValidate>
                <div className="form-row">
                  <label>
                    Full name
                    <input type="text" value={addressForm.fullName} onChange={updateAddressField('fullName')} required />
                    {addressFieldErrors.FullName && (
                      <span className="field-error">{addressFieldErrors.FullName}</span>
                    )}
                  </label>
                  <label>
                    Phone (optional)
                    <input type="tel" value={addressForm.phoneNumber} onChange={updateAddressField('phoneNumber')} />
                  </label>
                </div>

                <label>
                  Address
                  <input type="text" value={addressForm.addressLine} onChange={updateAddressField('addressLine')} required />
                  {addressFieldErrors.AddressLine && (
                    <span className="field-error">{addressFieldErrors.AddressLine}</span>
                  )}
                </label>

                <div className="form-row">
                  <label>
                    City
                    <input type="text" value={addressForm.city} onChange={updateAddressField('city')} required />
                    {addressFieldErrors.City && <span className="field-error">{addressFieldErrors.City}</span>}
                  </label>
                  <label>
                    State (optional)
                    <input type="text" value={addressForm.state} onChange={updateAddressField('state')} />
                  </label>
                </div>

                <div className="form-row">
                  <label>
                    Postal code
                    <input type="text" value={addressForm.postalCode} onChange={updateAddressField('postalCode')} required />
                    {addressFieldErrors.PostalCode && (
                      <span className="field-error">{addressFieldErrors.PostalCode}</span>
                    )}
                  </label>
                  <label>
                    Country
                    <input type="text" value={addressForm.country} onChange={updateAddressField('country')} required />
                    {addressFieldErrors.Country && (
                      <span className="field-error">{addressFieldErrors.Country}</span>
                    )}
                  </label>
                </div>

                <button type="submit" className="button button-secondary" disabled={savingAddress}>
                  {savingAddress ? 'Saving...' : 'Save address'}
                </button>
              </form>
            )}
          </section>

          <section className="card">
            <h2>Payment</h2>
            <p className="muted">
              This is a test gateway. Any card is approved unless it ends in{' '}
              <code>0000</code>, which is declined so you can see the retry path.
            </p>

            <form className="form" onSubmit={onSubmitPayment} noValidate>
              <label>
                Card number
                <input
                  type="text"
                  inputMode="numeric"
                  value={cardNumber}
                  onChange={(event) => setCardNumber(event.target.value)}
                  required
                />
              </label>

              <label>
                Name on card (optional)
                <input
                  type="text"
                  value={cardHolderName}
                  onChange={(event) => setCardHolderName(event.target.value)}
                />
              </label>

              {checkoutError && <ErrorMessage message={checkoutError} />}

              <button
                type="submit"
                className="button button-primary button-block"
                disabled={submitting || !selectedAddressId}
              >
                {submitting ? 'Processing...' : `Pay ${formatCurrency(cart.subtotal)}`}
              </button>
            </form>
          </section>
        </div>

        <aside className="card cart-summary">
          <h2>Order summary</h2>
          <ul className="summary-items">
            {cart.items.map((item) => (
              <li key={item.id}>
                <span>
                  {item.quantity} x {item.productName}
                </span>
                <span>{formatCurrency(item.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <dl>
            <div className="cart-summary-total">
              <dt>Subtotal</dt>
              <dd>{formatCurrency(cart.subtotal)}</dd>
            </div>
          </dl>
          <p className="muted">
            Order status starts as {orderStatusLabel(PENDING)} until the payment is approved.
          </p>
        </aside>
      </div>
    </div>
  );
}
