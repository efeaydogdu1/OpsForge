import Link from "next/link";

const modules = [
  { href: "/dashboard/teams", label: "Teams", desc: "Manage engineering teams and membership." },
  { href: "/dashboard/services", label: "Services", desc: "Register and manage platform services." },
  { href: "/dashboard/environments", label: "Environments", desc: "Track dev, test, UAT, and production environments." },
  { href: "/dashboard/infrastructure", label: "Infrastructure", desc: "Inventory databases, caches, VMs, and more." },
  { href: "/dashboard/deployments", label: "Deployments", desc: "Record and review deployment history." },
  { href: "/dashboard/audit", label: "Audit Log", desc: "Full audit trail of all platform actions." },
];

export default function DashboardPage() {
  return (
    <div>
      <h1 className="text-2xl font-semibold text-white mb-2">Dashboard</h1>
      <p className="text-slate-400 mb-8">Welcome to OpsForge. Choose a module to get started.</p>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {modules.map((m) => (
          <Link key={m.href} href={m.href}
            className="rounded-xl border border-white/10 bg-white/5 p-5 hover:border-cyan-500/50 hover:bg-cyan-600/10 transition-colors group">
            <p className="text-base font-medium text-white group-hover:text-cyan-300">{m.label}</p>
            <p className="mt-1 text-sm text-slate-400">{m.desc}</p>
          </Link>
        ))}
      </div>
    </div>
  );
}
