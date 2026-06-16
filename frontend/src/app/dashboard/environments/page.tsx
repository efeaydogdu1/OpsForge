"use client";
import { useEffect, useState } from "react";
import { environmentsApi, servicesApi } from "@/lib/api";

interface Env { id: string; serviceId: string; name: string; kind: string; url?: string; isDeleted: boolean; }
interface Service { id: string; name: string; }

const KINDS = ["Development", "Test", "Uat", "Production"];

export default function EnvironmentsPage() {
  const [envs, setEnvs] = useState<Env[]>([]);
  const [services, setServices] = useState<Service[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Env | null>(null);
  const [form, setForm] = useState({ serviceId: "", name: "", kind: "Development", url: "" });
  const [error, setError] = useState("");

  const load = async () => { setEnvs(await environmentsApi.list()); setServices(await servicesApi.list()); };
  useEffect(() => { load(); }, []);

  const openCreate = () => { setEditing(null); setForm({ serviceId: "", name: "", kind: "Development", url: "" }); setShowForm(true); setError(""); };
  const openEdit = (e: Env) => { setEditing(e); setForm({ serviceId: e.serviceId, name: e.name, kind: e.kind, url: e.url ?? "" }); setShowForm(true); setError(""); };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setError("");
    const kindIndex = KINDS.indexOf(form.kind);
    try {
      if (editing) await environmentsApi.update(editing.id, { name: form.name, kind: kindIndex, url: form.url });
      else await environmentsApi.create({ ...form, kind: kindIndex });
      setShowForm(false); load();
    } catch { setError("Operation failed."); }
  };

  const del = async (id: string) => { if (!confirm("Delete?")) return; await environmentsApi.delete(id); load(); };
  const serviceName = (id: string) => services.find((s) => s.id === id)?.name ?? id.slice(0, 8);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-white">Environments</h1>
        <button onClick={openCreate} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500">+ New Environment</button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5 space-y-4">
          <h2 className="font-medium text-white">{editing ? "Edit Environment" : "Create Environment"}</h2>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            <div><label className="mb-1 block text-sm text-slate-300">Service *</label>
              <select required value={form.serviceId} onChange={(e) => setForm({ ...form, serviceId: e.target.value })} className="input">
                <option value="">Select…</option>
                {services.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Name *</label><input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="input" /></div>
            <div><label className="mb-1 block text-sm text-slate-300">Kind</label>
              <select value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })} className="input">
                {KINDS.map((k) => <option key={k}>{k}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">URL</label><input value={form.url} onChange={(e) => setForm({ ...form, url: e.target.value })} className="input" /></div>
          </div>
          <div className="flex gap-3">
            <button type="submit" className="rounded-lg bg-cyan-600 px-4 py-2 text-sm text-white hover:bg-cyan-500">Save</button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-300">Cancel</button>
          </div>
        </form>
      )}

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left"><tr><th className="px-4 py-3 text-slate-400">Name</th><th className="px-4 py-3 text-slate-400">Service</th><th className="px-4 py-3 text-slate-400">Kind</th><th className="px-4 py-3 text-slate-400">URL</th><th className="px-4 py-3"></th></tr></thead>
          <tbody>
            {envs.map((e) => (
              <tr key={e.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-white">{e.name}</td>
                <td className="px-4 py-3 text-slate-400">{serviceName(e.serviceId)}</td>
                <td className="px-4 py-3"><span className="rounded-full bg-blue-500/10 px-2 py-0.5 text-xs text-blue-300">{e.kind}</span></td>
                <td className="px-4 py-3 text-slate-400 text-xs">{e.url ?? "—"}</td>
                <td className="px-4 py-3 flex gap-2 justify-end">
                  <button onClick={() => openEdit(e)} className="text-xs text-cyan-400 hover:underline">Edit</button>
                  <button onClick={() => del(e.id)} className="text-xs text-red-400 hover:underline">Delete</button>
                </td>
              </tr>
            ))}
            {envs.length === 0 && <tr><td colSpan={5} className="px-4 py-6 text-center text-slate-500">No environments yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
