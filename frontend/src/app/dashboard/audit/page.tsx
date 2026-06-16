"use client";
import { useEffect, useState } from "react";
import { auditApi } from "@/lib/api";

interface AuditLog { id: string; userId?: string; timestampUtc: string; action: string; entityType: string; entityId: string; details?: string; }

const ACTION_COLORS: Record<string, string> = {
  Login: "bg-blue-500/10 text-blue-300",
  Create: "bg-green-500/10 text-green-300",
  Update: "bg-amber-500/10 text-amber-300",
  Delete: "bg-red-500/10 text-red-300",
  Deployment: "bg-purple-500/10 text-purple-300",
};

export default function AuditPage() {
  const [logs, setLogs] = useState<AuditLog[]>([]);
  const [filterType, setFilterType] = useState("");

  const load = async () => setLogs(await auditApi.list({ entityType: filterType || undefined }));
  useEffect(() => { load(); }, [filterType]);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-white">Audit Log</h1>
        <input placeholder="Filter by entity type…" value={filterType} onChange={(e) => setFilterType(e.target.value)}
          className="rounded-lg border border-white/10 bg-white/5 px-3 py-1.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-500 w-52" />
      </div>

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left"><tr><th className="px-4 py-3 text-slate-400">Timestamp</th><th className="px-4 py-3 text-slate-400">Action</th><th className="px-4 py-3 text-slate-400">Entity</th><th className="px-4 py-3 text-slate-400">Entity ID</th><th className="px-4 py-3 text-slate-400">Details</th></tr></thead>
          <tbody>
            {logs.map((l) => (
              <tr key={l.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-xs text-slate-400 whitespace-nowrap">{new Date(l.timestampUtc).toLocaleString()}</td>
                <td className="px-4 py-3"><span className={`rounded-full px-2 py-0.5 text-xs ${ACTION_COLORS[l.action] ?? "bg-white/5 text-slate-300"}`}>{l.action}</span></td>
                <td className="px-4 py-3 text-slate-300">{l.entityType}</td>
                <td className="px-4 py-3 font-mono text-xs text-slate-400">{l.entityId.slice(0, 8)}…</td>
                <td className="px-4 py-3 text-xs text-slate-500">{l.details ?? "—"}</td>
              </tr>
            ))}
            {logs.length === 0 && <tr><td colSpan={5} className="px-4 py-6 text-center text-slate-500">No audit entries.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
