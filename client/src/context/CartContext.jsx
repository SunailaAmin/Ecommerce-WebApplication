import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { api } from '../lib/api';
import { useAuth } from './AuthContext';

const CartContext = createContext(null);

export function CartProvider({ children }) {
  const { isAuthenticated } = useAuth();
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const refresh = useCallback(async () => {
    if (!isAuthenticated) {
      setCart(null);
      return null;
    }

    setLoading(true);
    setError(null);
    try {
      const result = await api.cart();
      setCart(result);
      return result;
    } catch (err) {
      setError(err.message);
      return null;
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  // Every mutation goes through the server so the returned cart is authoritative.
  const run = useCallback(async (action) => {
    setLoading(true);
    setError(null);
    try {
      const result = await action();
      setCart(result);
      return result;
    } catch (err) {
      setError(err.message);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const value = useMemo(
    () => ({
      cart,
      loading,
      error,
      totalQuantity: cart?.totalQuantity ?? 0,
      refresh,
      addItem: (productId, quantity) => run(() => api.addToCart({ productId, quantity })),
      updateItem: (itemId, quantity) => run(() => api.updateCartItem(itemId, quantity)),
      removeItem: (itemId) => run(() => api.removeCartItem(itemId)),
      clear: () => run(() => api.clearCart()),
      setError,
    }),
    [cart, loading, error, refresh, run],
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart() {
  const context = useContext(CartContext);
  if (!context) throw new Error('useCart must be used inside CartProvider');
  return context;
}
