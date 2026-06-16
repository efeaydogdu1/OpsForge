"use client";
import React, { createContext, useCallback, useContext, useEffect, useState } from "react";
import { authApi } from "@/lib/api";

interface User { userId: string; email: string; displayName: string; role: string; }
interface AuthCtx { user: User | null; loading: boolean; login(email: string, password: string): Promise<void>; register(email: string, password: string, displayName: string): Promise<void>; logout(): void; }

const AuthContext = createContext<AuthCtx | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem("opsforge.token");
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split(".")[1]));
        setUser({ userId: payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"], email: payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"], displayName: payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"], role: payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] });
      } catch { localStorage.removeItem("opsforge.token"); }
    }
    setLoading(false);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const data = await authApi.login({ email, password });
    localStorage.setItem("opsforge.token", data.accessToken);
    setUser({ userId: data.userId, email: data.email, displayName: data.displayName, role: data.role });
  }, []);

  const register = useCallback(async (email: string, password: string, displayName: string) => {
    const data = await authApi.register({ email, password, displayName });
    localStorage.setItem("opsforge.token", data.accessToken);
    setUser({ userId: data.userId, email: data.email, displayName: data.displayName, role: data.role });
  }, []);

  const logout = useCallback(() => {
    authApi.logout();
    setUser(null);
  }, []);

  return <AuthContext.Provider value={{ user, loading, login, register, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
