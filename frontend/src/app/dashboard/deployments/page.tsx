"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { deploymentsApi, environmentsApi, servicesApi } from "@/lib/api";

interface Deployment { id: string; serviceId: string; environmentId: string; version: string; commitHash: string; releaseNotes?: string; deploymentDateUtc: string; deployedByUserId: string; }
interface Service { id: string; name: string; }
interface Env { id: string; name: string; serviceId: string; }

export default function DeploymentsPage() {
  const [deployments, setDeployments] = useState<Deployment[]>([]);
  const [services, setServices] = useState<Service[]>([]);
  const [envs, setEnvs] = useState<Env[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ serviceId: "", environmentId: "", version: "", commitHash: "", releaseNotes: "" });
  const [error, setError] = useState("");

  const load = async () => { setDeployments(await deploymentsApi.list()); setServices(await servicesApi.list()); setEnvs(await environmentsApi.list()); };
  useEffect(() => { load(); }, []);

  const filteredEnvs = envs.filter((e) => !form.serviceId || e.serviceId === form.serviceId);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setError("");
    try { await deploymentsApi.create(form); setShowForm(false); load(); }
    catch { setError("Deployment failed. Check credentials and required fields."); }
  };

  const serviceName = (id: string) => services.find((s) => s.id === id)?.name ?? "—";
  const envName = (id: string) => envs.find((e) => e.id === id)?.name ?? "—";

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-white">Deployments</h1>
        <button onClick={() => { setShowForm(true); setForm({ serviceId: "", environmentId: "", version: "", commitHash: "", releaseNotes: "" }); setError(""); }} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500">+ Record Deployment</button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5 space-y-4">
          <h2 className="font-medium text-white">Record Deployment</h2>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            <div><label className="mb-1 block text-sm text-slate-300">Service *</label>
              <select required value={form.serviceId} onChange={(e) => setForm({ ...form, serviceId: e.target.value, environmentId: "" })} className="input">
                <option value="">Select…</option>
                {services.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Environment *</label>
              <select required value={form.environmentId} onChange={(e) => setForm({ ...form, environmentId: e.target.value })} className="input">
                <option value="">Select…</option>
                {filteredEnvs.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Version *</label><input required value={form.version} onChange={(e) => setForm({ ...form, version: e.target.value })} className="input" placeholder="1.2.3" /></div>
            <div><label className="mb-1 block text-sm text-slate-300">Commit Hash *</label><input required value={form.commitHash} onChange={(e) => setForm({ ...form, commitHash: e.target.value })} className="input" placeholder="abc123" /></div>
          </div>
          <div><label className="mb-1 block text-sm text-slate-300">Release Notes</label><textarea rows={3} value={form.releaseNotes} onChange={(e) => setForm({ ...form, releaseNotes: e.target.value })} className="input" /></div>
          <div className="flex gap-3">
            <button type="submit" className="rounded-lg bg-cyan-600 px-4 py-2 text-sm text-white hover:bg-cyan-500">Record</button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-300">Cancel</button>
          </div>
        </form>
      )}

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left"><tr><th className="px-4 py-3 text-slate-400">Service</th><th className="px-4 py-3 text-slate-400">Environment</th><th className="px-4 py-3 text-slate-400">Version</th><th className="px-4 py-3 text-slate-400">Commit</th><th className="px-4 py-3 text-slate-400">Date</th></tr></thead>
          <tbody>
            {deployments.map((d) => (
              <tr key={d.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-white">
                  <Link href={`/dashboard/infrastructure?serviceId=${d.serviceId}`} className="hover:text-cyan-300 hover:underline">
                    {serviceName(d.serviceId)}
                  </Link>
                </td>
                <td className="px-4 py-3 text-slate-400">{envName(d.environmentId)}</td>
                <td className="px-4 py-3"><span className="rounded-full bg-cyan-500/10 px-2 py-0.5 text-xs text-cyan-300">v{d.version}</span></td>
                <td className="px-4 py-3 font-mono text-xs text-slate-400">{d.commitHash.slice(0, 7)}</td>
                <td className="px-4 py-3 text-xs text-slate-400">{new Date(d.deploymentDateUtc).toLocaleString()}</td>
              </tr>
            ))}
            {deployments.length === 0 && <tr><td colSpan={5} className="px-4 py-6 text-center text-slate-500">No deployments yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
