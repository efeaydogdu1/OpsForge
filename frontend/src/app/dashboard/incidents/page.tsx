"use client";
import { useEffect, useState } from "react";
import { deploymentsApi, environmentsApi, incidentsApi, servicesApi } from "@/lib/api";

interface Incident {
  id: string;
  title: string;
  description: string;
  severity: string;
  status: string;
  serviceId: string;
  serviceName: string;
  serviceGitHubUrl?: string;
  environmentId?: string;
  environmentName?: string;
  environmentGitHubUrl?: string;
  deploymentId?: string;
  deploymentVersion?: string;
  deploymentGitHubUrl?: string;
  occurredAtUtc: string;
  resolvedAtUtc?: string;
}
interface Service { id: string; name: string; }
interface Env { id: string; serviceId: string; name: string; }
interface Deployment { id: string; serviceId: string; environmentId: string; version: string; commitHash: string; }

const SEVERITIES = ["Low", "Medium", "High", "Critical"];
const STATUSES = ["Open", "Investigating", "Mitigated", "Resolved"];
const emptyForm = { title: "", description: "", severity: "Medium", status: "Open", serviceId: "", environmentId: "", deploymentId: "" };

export default function IncidentsPage() {
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [services, setServices] = useState<Service[]>([]);
  const [envs, setEnvs] = useState<Env[]>([]);
  const [deployments, setDeployments] = useState<Deployment[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Incident | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState("");

  const load = async () => {
    const [nextIncidents, nextServices, nextEnvs, nextDeployments] = await Promise.all([
      incidentsApi.list(),
      servicesApi.list(),
      environmentsApi.list(),
      deploymentsApi.list(),
    ]);
    setIncidents(nextIncidents);
    setServices(nextServices);
    setEnvs(nextEnvs);
    setDeployments(nextDeployments);
  };

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      const [nextIncidents, nextServices, nextEnvs, nextDeployments] = await Promise.all([
        incidentsApi.list(),
        servicesApi.list(),
        environmentsApi.list(),
        deploymentsApi.list(),
      ]);
      if (!cancelled) {
        setIncidents(nextIncidents);
        setServices(nextServices);
        setEnvs(nextEnvs);
        setDeployments(nextDeployments);
      }
    };

    void initialize();

    return () => {
      cancelled = true;
    };
  }, []);

  const filteredEnvs = envs.filter((e) => !form.serviceId || e.serviceId === form.serviceId);
  const filteredDeployments = deployments.filter((d) =>
    (!form.serviceId || d.serviceId === form.serviceId) &&
    (!form.environmentId || d.environmentId === form.environmentId)
  );

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm);
    setShowForm(true);
    setError("");
  };

  const openEdit = (item: Incident) => {
    setEditing(item);
    setForm({
      title: item.title,
      description: item.description,
      severity: item.severity,
      status: item.status,
      serviceId: item.serviceId,
      environmentId: item.environmentId ?? "",
      deploymentId: item.deploymentId ?? "",
    });
    setShowForm(true);
    setError("");
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    const payload = {
      title: form.title,
      description: form.description,
      severity: SEVERITIES.indexOf(form.severity) + 1,
      status: STATUSES.indexOf(form.status) + 1,
      serviceId: form.serviceId,
      environmentId: form.environmentId || null,
      deploymentId: form.deploymentId || null,
      resolvedAtUtc: form.status === "Resolved" ? new Date().toISOString() : null,
    };

    try {
      if (editing) await incidentsApi.update(editing.id, payload);
      else await incidentsApi.create(payload);
      setShowForm(false);
      await load();
    } catch {
      setError("Operation failed. Check the linked service, environment, and CI/CD process.");
    }
  };

  const del = async (id: string) => {
    if (!confirm("Delete this incident?")) return;
    await incidentsApi.delete(id);
    await load();
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-white">Incidents</h1>
        <button onClick={openCreate} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500">+ New Incident</button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5 space-y-4">
          <h2 className="font-medium text-white">{editing ? "Edit Incident" : "Create Incident"}</h2>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            <div><label className="mb-1 block text-sm text-slate-300">Title *</label><input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} className="input" placeholder="Checkout API latency spike" /></div>
            <div><label className="mb-1 block text-sm text-slate-300">Service *</label>
              <select required value={form.serviceId} onChange={(e) => setForm({ ...form, serviceId: e.target.value, environmentId: "", deploymentId: "" })} className="input">
                <option value="">Select...</option>
                {services.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Environment</label>
              <select value={form.environmentId} onChange={(e) => setForm({ ...form, environmentId: e.target.value, deploymentId: "" })} className="input">
                <option value="">None</option>
                {filteredEnvs.map((env) => <option key={env.id} value={env.id}>{env.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">CI/CD Process</label>
              <select value={form.deploymentId} onChange={(e) => setForm({ ...form, deploymentId: e.target.value })} className="input">
                <option value="">None</option>
                {filteredDeployments.map((d) => <option key={d.id} value={d.id}>{d.version} / {d.commitHash.slice(0, 7)}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Severity</label>
              <select value={form.severity} onChange={(e) => setForm({ ...form, severity: e.target.value })} className="input">
                {SEVERITIES.map((s) => <option key={s}>{s}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Status</label>
              <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })} className="input">
                {STATUSES.map((s) => <option key={s}>{s}</option>)}
              </select>
            </div>
          </div>
          <div><label className="mb-1 block text-sm text-slate-300">What happened? *</label><textarea required rows={4} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className="input" /></div>
          <div className="flex gap-3">
            <button type="submit" className="rounded-lg bg-cyan-600 px-4 py-2 text-sm text-white hover:bg-cyan-500">Save</button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-300">Cancel</button>
          </div>
        </form>
      )}

      <div className="space-y-3">
        {incidents.map((incident) => (
          <div key={incident.id} className="rounded-xl border border-white/10 bg-white/5 p-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="text-lg font-medium text-white">{incident.title}</h2>
                  <span className="rounded-full bg-amber-500/10 px-2 py-0.5 text-xs text-amber-300">{incident.severity}</span>
                  <span className="rounded-full bg-cyan-500/10 px-2 py-0.5 text-xs text-cyan-300">{incident.status}</span>
                </div>
                <p className="mt-2 text-sm text-slate-300">{incident.description}</p>
              </div>
              <div className="flex gap-2">
                <button onClick={() => openEdit(incident)} className="text-xs text-cyan-400 hover:underline">Edit</button>
                <button onClick={() => del(incident.id)} className="text-xs text-red-400 hover:underline">Delete</button>
              </div>
            </div>

            <div className="mt-4 grid gap-3 md:grid-cols-3">
              <ResourceLink label="Service" name={incident.serviceName} url={incident.serviceGitHubUrl} />
              <ResourceLink label="Environment" name={incident.environmentName ?? "-"} url={incident.environmentGitHubUrl} />
              <ResourceLink label="CI/CD Process" name={incident.deploymentVersion ?? "-"} url={incident.deploymentGitHubUrl} />
            </div>
            <p className="mt-4 text-xs text-slate-500">{new Date(incident.occurredAtUtc).toLocaleString()}</p>
          </div>
        ))}
        {incidents.length === 0 && <div className="rounded-xl border border-white/10 py-8 text-center text-sm text-slate-500">No incidents yet.</div>}
      </div>
    </div>
  );
}

function ResourceLink({ label, name, url }: { label: string; name: string; url?: string }) {
  return (
    <div className="rounded-lg border border-white/10 bg-slate-950/30 p-3">
      <p className="text-xs uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-1 text-sm text-white">{name}</p>
      {url ? (
        <a href={url} target="_blank" rel="noreferrer" className="mt-2 inline-block text-xs text-cyan-300 hover:underline">Open GitHub</a>
      ) : (
        <p className="mt-2 text-xs text-slate-500">No GitHub link</p>
      )}
    </div>
  );
}
