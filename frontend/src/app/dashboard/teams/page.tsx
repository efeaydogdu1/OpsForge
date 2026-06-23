"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { servicesApi, teamsApi } from "@/lib/api";

interface Team { id: string; name: string; description?: string; isDeleted: boolean; }
interface Service { id: string; name: string; ownerTeamId?: string; }

export default function TeamsPage() {
  const [teams, setTeams] = useState<Team[]>([]);
  const [services, setServices] = useState<Service[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Team | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState("");

  const load = async () => {
    const [teamList, serviceList] = await Promise.all([teamsApi.list(), servicesApi.list()]);
    setTeams(teamList);
    setServices(serviceList);
  };

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      const [teamList, serviceList] = await Promise.all([teamsApi.list(), servicesApi.list()]);
      if (!cancelled) {
        setTeams(teamList);
        setServices(serviceList);
      }
    };

    void initialize();

    return () => {
      cancelled = true;
    };
  }, []);

  const openCreate = () => { setEditing(null); setName(""); setDescription(""); setShowForm(true); setError(""); };
  const openEdit = (t: Team) => { setEditing(t); setName(t.name); setDescription(t.description ?? ""); setShowForm(true); setError(""); };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      if (editing) await teamsApi.update(editing.id, { name, description });
      else await teamsApi.create({ name, description });
      setShowForm(false);
      load();
    } catch { setError("Operation failed."); }
  };

  const del = async (id: string) => {
    if (!confirm("Soft-delete this team?")) return;
    await teamsApi.delete(id);
    load();
  };

  const teamServices = (teamId: string) => services.filter((service) => service.ownerTeamId === teamId);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-white">Teams</h1>
        <button onClick={openCreate} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500">+ New Team</button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5 space-y-4">
          <h2 className="font-medium text-white">{editing ? "Edit Team" : "Create Team"}</h2>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <div><label className="mb-1 block text-sm text-slate-300">Name *</label>
            <input required value={name} onChange={(e) => setName(e.target.value)} className="input" /></div>
          <div><label className="mb-1 block text-sm text-slate-300">Description</label>
            <input value={description} onChange={(e) => setDescription(e.target.value)} className="input" /></div>
          <div className="flex gap-3">
            <button type="submit" className="rounded-lg bg-cyan-600 px-4 py-2 text-sm text-white hover:bg-cyan-500">Save</button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-300 hover:text-white">Cancel</button>
          </div>
        </form>
      )}

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left">
            <tr><th className="px-4 py-3 text-slate-400">Name</th><th className="px-4 py-3 text-slate-400">Description</th><th className="px-4 py-3 text-slate-400">Services</th><th className="px-4 py-3 text-slate-400">Status</th><th className="px-4 py-3"></th></tr>
          </thead>
          <tbody>
            {teams.map((t) => (
              <tr key={t.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-white">
                  <Link href={`/dashboard/services?teamId=${t.id}`} className="hover:text-cyan-300 hover:underline">{t.name}</Link>
                </td>
                <td className="px-4 py-3 text-slate-400">{t.description ?? "—"}</td>
                <td className="px-4 py-3 text-slate-300">
                  <div className="flex flex-wrap gap-2">
                    {teamServices(t.id).length === 0 && <span className="text-xs text-slate-500">None</span>}
                    {teamServices(t.id).map((service) => (
                      <Link key={service.id} href={`/dashboard/services?serviceId=${service.id}`} className="rounded-full bg-cyan-500/10 px-2 py-0.5 text-xs text-cyan-300 hover:bg-cyan-500/20">
                        {service.name}
                      </Link>
                    ))}
                  </div>
                </td>
                <td className="px-4 py-3"><span className={`rounded-full px-2 py-0.5 text-xs ${t.isDeleted ? "bg-red-500/10 text-red-400" : "bg-green-500/10 text-green-400"}`}>{t.isDeleted ? "Deleted" : "Active"}</span></td>
                <td className="px-4 py-3 flex gap-2 justify-end">
                  <button onClick={() => openEdit(t)} className="text-xs text-cyan-400 hover:underline">Edit</button>
                  <button onClick={() => del(t.id)} className="text-xs text-red-400 hover:underline">Delete</button>
                </td>
              </tr>
            ))}
            {teams.length === 0 && <tr><td colSpan={5} className="px-4 py-6 text-center text-slate-500">No teams yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
