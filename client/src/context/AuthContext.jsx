import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import {
  api,
  clearSession,
  getToken,
  hasValidToken,
  onUnauthorized,
  saveSession,
  setTokenProvider,
} from '../lib/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [restoring, setRestoring] = useState(hasValidToken());

  const signOut = useCallback(() => {
    clearSession();
    setUser(null);
  }, []);

  useEffect(() => {
    setTokenProvider(getToken);
  }, []);

  useEffect(() => {
    onUnauthorized(signOut);
    return () => onUnauthorized(null);
  }, [signOut]);

  // A token survives a refresh, so the profile is re-fetched rather than trusted.
  useEffect(() => {
    let cancelled = false;

    if (!hasValidToken()) {
      clearSession();
      setRestoring(false);
      return () => {};
    }

    api
      .me()
      .then((profile) => {
        if (!cancelled) setUser(profile);
      })
      .catch(() => {
        if (!cancelled) signOut();
      })
      .finally(() => {
        if (!cancelled) setRestoring(false);
      });

    return () => {
      cancelled = true;
    };
  }, [signOut]);

  const applyAuth = useCallback((auth) => {
    saveSession(auth.token, auth.expiresAt);
    setUser(auth.user);
    return auth.user;
  }, []);

  const value = useMemo(
    () => ({
      user,
      restoring,
      isAuthenticated: Boolean(user),
      isAdmin: user?.role === 'Admin',
      signIn: async (email, password) => applyAuth(await api.login({ email, password })),
      signUp: async (details) => applyAuth(await api.register(details)),
      signOut,
    }),
    [user, restoring, applyAuth, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside AuthProvider');
  return context;
}
