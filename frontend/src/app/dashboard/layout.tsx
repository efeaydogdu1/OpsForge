"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/context/AuthContext";
import RequireAuth from "@/components/RequireAuth";

const NAV = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/dashboard/teams", label: "Teams" },
  { href: "/dashboard/services", label: "Services" },
  { href: "/dashboard/environments", label: "Environments" },
  { href: "/dashboard/infrastructure", label: "Infrastructure" },
  { href: "/dashboard/deployments", label: "Deployments" },
  { href: "/dashboard/audit", label: "Audit Log" },
];

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { user, logout } = useAuth();
  const pathname = usePathname();

  return (
    <RequireAuth>
      <div className="flex min-h-screen bg-slate-950 text-slate-100">
        <aside className="flex w-56 flex-col border-r border-white/10 bg-slate-900">
          <div className="px-5 py-5 border-b border-white/10">
            <p className="text-xs uppercase tracking-widest text-cyan-400">OpsForge</p>
          </div>
          <nav className="flex-1 py-4 space-y-1 px-2">
            {NAV.map((n) => (
              <Link key={n.href} href={n.href}
                className={`block rounded-lg px-3 py-2 text-sm transition-colors ${pathname === n.href ? "bg-cyan-600/20 text-cyan-300" : "text-slate-400 hover:text-white hover:bg-white/5"}`}>
                {n.label}
              </Link>
            ))}
          </nav>
          <div className="px-4 py-4 border-t border-white/10 text-xs text-slate-500">
            <p className="truncate">{user?.displayName}</p>
            <p className="truncate text-slate-600">{user?.email}</p>
            <button onClick={logout} className="mt-2 text-red-400 hover:text-red-300">Sign out</button>
          </div>
        </aside>
        <main className="flex-1 overflow-y-auto p-8">{children}</main>
      </div>
    </RequireAuth>
  );
}
