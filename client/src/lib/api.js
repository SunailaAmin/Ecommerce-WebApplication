// Single place that knows how to talk to the API: the base URL, the JWT, and how a
// failed response becomes an error the UI can show.

const API_BASE = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

const TOKEN_KEY = 'shopfront.token';
const EXPIRY_KEY = 'shopfront.expiresAt';

let unauthorizedHandler = null;
let tokenProvider = () => null;

/** Called by the auth layer so a rejected token can clear the session. */
export function onUnauthorized(handler) {
  unauthorizedHandler = handler;
}

export function setTokenProvider(provider) {
  tokenProvider = provider;
}

export function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function saveSession(token, expiresAt) {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(EXPIRY_KEY, expiresAt ?? '');
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(EXPIRY_KEY);
}

/** True when there is a token that has not passed its expiry. */
export function hasValidToken() {
  const token = getToken();
  if (!token) return false;

  const expiresAt = localStorage.getItem(EXPIRY_KEY);
  if (expiresAt && new Date(expiresAt) <= new Date()) {
    return false;
  }

  return true;
}

export class ApiError extends Error {
  constructor(message, { status, fieldErrors } = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.fieldErrors = fieldErrors ?? {};
  }
}

/**
 * Turns a ProblemDetails or ValidationProblemDetails body into a message plus any
 * per-field messages, so a form can point at the input that caused the failure.
 */
function toApiError(status, body) {
  if (!body || typeof body !== 'object') {
    return new ApiError(defaultMessage(status), { status });
  }

  const fieldErrors = {};
  for (const [field, messages] of Object.entries(body.errors ?? {})) {
    if (Array.isArray(messages)) fieldErrors[field] = messages.join(' ');
  }

  const firstFieldMessage = Object.values(fieldErrors)[0];
  const message =
    body.detail ||
    (firstFieldMessage ? null : body.title) ||
    defaultMessage(status);

  return new ApiError(message || firstFieldMessage || defaultMessage(status), { status, fieldErrors });
}

function defaultMessage(status) {
  if (status === 401) return 'Your session has expired. Please sign in again.';
  if (status === 403) return 'You do not have permission to do that.';
  if (status === 404) return 'That item could not be found.';
  if (status === 409) return 'That change conflicts with the current state.';
  if (status >= 500) return 'Something went wrong on the server.';
  return 'The request could not be completed.';
}

function buildUrl(path, params) {
  const url = `${API_BASE}${path}`;

  if (!params) return url;

  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    search.append(key, String(value));
  }

  const query = search.toString();
  return query ? `${url}?${query}` : url;
}

async function request(path, { method = 'GET', body, params, auth = true } = {}) {
  const headers = { Accept: 'application/json' };
  const token = tokenProvider();

  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (auth && token) headers.Authorization = `Bearer ${token}`;

  let response;
  try {
    response = await fetch(buildUrl(path, params), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError('Could not reach the server. Check your connection and try again.');
  }

  if (response.status === 204) return null;

  const text = await response.text();
  let payload = null;
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = text;
    }
  }

  if (!response.ok) {
    if (response.status === 401 && auth && token) unauthorizedHandler?.();

    throw toApiError(response.status, payload);
  }

  return payload;
}

export const api = {
  // Auth
  register: (body) => request('/api/auth/register', { method: 'POST', body, auth: false }),
  login: (body) => request('/api/auth/login', { method: 'POST', body, auth: false }),
  me: () => request('/api/auth/me'),

  // Catalog
  products: (params) => request('/api/products', { params }),
  product: (id) => request(`/api/products/${id}`),
  categories: (params) => request('/api/categories', { params }),

  // Cart
  cart: () => request('/api/cart'),
  addToCart: (body) => request('/api/cart/items', { method: 'POST', body }),
  updateCartItem: (itemId, quantity) =>
    request(`/api/cart/items/${itemId}`, { method: 'PUT', body: { quantity } }),
  removeCartItem: (itemId) => request(`/api/cart/items/${itemId}`, { method: 'DELETE' }),
  clearCart: () => request('/api/cart', { method: 'DELETE' }),

  // Addresses
  addresses: () => request('/api/addresses'),
  createAddress: (body) => request('/api/addresses', { method: 'POST', body }),

  // Checkout and payments
  checkout: (body) => request('/api/checkout', { method: 'POST', body }),
  payOrder: (body) => request('/api/payments', { method: 'POST', body }),

  // Orders
  orders: (params) => request('/api/orders', { params }),
  order: (id) => request(`/api/orders/${id}`),
  orderPayment: (id) => request(`/api/orders/${id}/payment`),
  createOrder: (body) => request('/api/orders', { method: 'POST', body }),
};

export { API_BASE };
