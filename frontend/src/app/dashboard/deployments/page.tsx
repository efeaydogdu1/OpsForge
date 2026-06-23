"use client";
import { useEffect, useState } from "react";
import { deploymentsApi, environmentsApi, servicesApi } from "@/lib/api";

interface Deployment { id: string; serviceId: string; environmentId: string; version: string; commitHash: string; releaseNotes?: string; deploymentDateUtc: string; deployedByUserId: string; }
interface Service { id: string; name: string; repositoryUrl?: string; }
interface Env { id: string; name: string; serviceId: string; }

const emptyForm = { serviceId: "", environmentId: "", version: "", commitHash: "", releaseNotes: "" };

export default function DeploymentsPage() {
  const [deployments, setDeployments] = useState<Deployment[]>([]);
  const [services, setServices] = useState<Service[]>([]);
  const [envs, setEnvs] = useState<Env[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Deployment | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState("");

  const load = async () => {
    const [nextDeployments, nextServices, nextEnvs] = await Promise.all([
      deploymentsApi.list(),
      servicesApi.list(),
      environmentsApi.list(),
    ]);
    setDeployments(nextDeployments);
    setServices(nextServices);
    setEnvs(nextEnvs);
  };

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      const [nextDeployments, nextServices, nextEnvs] = await Promise.all([
        deploymentsApi.list(),
        servicesApi.list(),
        environmentsApi.list(),
      ]);
      if (!cancelled) {
        setDeployments(nextDeployments);
        setServices(nextServices);
        setEnvs(nextEnvs);
      }
    };

    void initialize();

    return () => {
      cancelled = true;
    };
  }, []);

  const filteredEnvs = envs.filter((e) => !form.serviceId || e.serviceId === form.serviceId);

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm);
    setShowForm(true);
    setError("");
  };

  const openEdit = (item: Deployment) => {
    setEditing(item);
    setForm({
      serviceId: item.serviceId,
      environmentId: item.environmentId,
      version: item.version,
      commitHash: item.commitHash,
      releaseNotes: item.releaseNotes ?? "",
    });
    setShowForm(true);
    setError("");
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      if (editing) await deploymentsApi.update(editing.id, form);
      else await deploymentsApi.create(form);
      setShowForm(false);
      await load();
    } catch {
      setError("Operation failed. Check credentials and required fields.");
    }
  };

  const del = async (id: string) => {
    if (!confirm("Delete this CI/CD process?")) return;
    await deploymentsApi.delete(id);
    await load();
  };

  const serviceName = (id: string) => services.find((s) => s.id === id)?.name ?? "-";
  const envName = (id: string) => envs.find((e) => e.id === id)?.name ?? "-";
  const commitUrl = (item: Deployment) => {
    const repositoryUrl = services.find((s) => s.id === item.serviceId)?.repositoryUrl;
    return repositoryUrl ? `${repositoryUrl}/commit/${item.commitHash}` : null;
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-white">CI/CD Processes</h1>
        <button onClick={openCreate} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500">+ New Process</button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5 space-y-4">
          <h2 className="font-medium text-white">{editing ? "Edit CI/CD Process" : "Record CI/CD Process"}</h2>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            <div><label className="mb-1 block text-sm text-slate-300">Service *</label>
              <select required value={form.serviceId} onChange={(e) => setForm({ ...form, serviceId: e.target.value, environmentId: "" })} className="input">
                <option value="">Select...</option>
                {services.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Environment *</label>
              <select required value={form.environmentId} onChange={(e) => setForm({ ...form, environmentId: e.target.value })} className="input">
                <option value="">Select...</option>
                {filteredEnvs.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Ref / Version *</label><input required value={form.version} onChange={(e) => setForm({ ...form, version: e.target.value })} className="input" placeholder="main or 1.2.3" /></div>
            <div><label className="mb-1 block text-sm text-slate-300">Commit SHA *</label><input required value={form.commitHash} onChange={(e) => setForm({ ...form, commitHash: e.target.value })} className="input" placeholder="abc123" /></div>
          </div>
          <div><label className="mb-1 block text-sm text-slate-300">Notes</label><textarea rows={3} value={form.releaseNotes} onChange={(e) => setForm({ ...form, releaseNotes: e.target.value })} className="input" /></div>
          <div className="flex gap-3">
            <button type="submit" className="rounded-lg bg-cyan-600 px-4 py-2 text-sm text-white hover:bg-cyan-500">Save</button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-300">Cancel</button>
          </div>
        </form>
      )}

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left">
            <tr><th className="px-4 py-3 text-slate-400">Service</th><th className="px-4 py-3 text-slate-400">Environment</th><th className="px-4 py-3 text-slate-400">Ref / Version</th><th className="px-4 py-3 text-slate-400">Commit</th><th className="px-4 py-3 text-slate-400">Date</th><th className="px-4 py-3"></th></tr>
          </thead>
          <tbody>
            {deployments.map((d) => (
              <tr key={d.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-white">{serviceName(d.serviceId)}</td>
                <td className="px-4 py-3 text-slate-400">{envName(d.environmentId)}</td>
                <td className="px-4 py-3"><span className="rounded-full bg-cyan-500/10 px-2 py-0.5 text-xs text-cyan-300">{d.version}</span></td>
                <td className="px-4 py-3 font-mono text-xs">
                  {commitUrl(d) ? (
                    <a href={commitUrl(d)!} target="_blank" rel="noreferrer" className="text-cyan-300 hover:underline">{d.commitHash.slice(0, 7)}</a>
                  ) : (
                    <span className="text-slate-400">{d.commitHash.slice(0, 7)}</span>
                  )}
                </td>
                <td className="px-4 py-3 text-xs text-slate-400">{new Date(d.deploymentDateUtc).toLocaleString()}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <button onClick={() => openEdit(d)} className="text-xs text-cyan-400 hover:underline">Edit</button>
                    <button onClick={() => del(d.id)} className="text-xs text-red-400 hover:underline">Delete</button>
                  </div>
                </td>
              </tr>
            ))}
            {deployments.length === 0 && <tr><td colSpan={6} className="px-4 py-6 text-center text-slate-500">No CI/CD processes yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
