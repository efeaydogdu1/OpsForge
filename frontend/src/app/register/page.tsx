"use client";
import { useState } from "react";
import { useAuth } from "@/context/AuthContext";
import Link from "next/link";
import { useRouter } from "next/navigation";

export default function RegisterPage() {
  const { register } = useAuth();
  const router = useRouter();
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      await register(email, password, displayName);
      router.push("/dashboard");
    } catch {
      setError("Registration failed. Email may already be in use.");
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-950 px-4">
      <div className="w-full max-w-sm rounded-2xl border border-white/10 bg-white/5 p-8 shadow-2xl">
        <h1 className="mb-6 text-2xl font-semibold text-white">Create account</h1>
        {error && <p className="mb-4 rounded-lg bg-red-500/10 px-4 py-2 text-sm text-red-400">{error}</p>}
        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm text-slate-300">Name</label>
            <input type="text" required value={displayName} onChange={(e) => setDisplayName(e.target.value)}
              className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-500" />
          </div>
          <div>
            <label className="mb-1 block text-sm text-slate-300">Email</label>
            <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-500" />
          </div>
          <div>
            <label className="mb-1 block text-sm text-slate-300">Password</label>
            <input type="password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-500" />
          </div>
          <button type="submit" className="w-full rounded-lg bg-cyan-600 py-2 font-medium text-white hover:bg-cyan-500 transition-colors">
            Register
          </button>
        </form>
        <p className="mt-4 text-center text-sm text-slate-400">
          Already have an account?{" "}
          <Link href="/login" className="text-cyan-400 hover:underline">Sign in</Link>
        </p>
      </div>
    </div>
  );
}
