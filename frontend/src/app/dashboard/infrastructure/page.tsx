"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { infraApi, servicesApi } from "@/lib/api";

interface Asset { id: string; name: string; assetType: string; provider?: string; resourceIdentifier?: string; isDeleted: boolean; linkedServiceIds: string[]; }
interface Service { id: string; name: string; }

const ASSET_TYPES = ["SqlDatabase", "Redis", "AppService", "VirtualMachine", "StorageAccount", "KeyVault"];

export default function InfrastructurePage() {
  const searchParams = useSearchParams();
  const selectedServiceFilter = searchParams.get("serviceId") ?? "";
  const [assets, setAssets] = useState<Asset[]>([]);
  const [services, setServices] = useState<Service[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Asset | null>(null);
  const [form, setForm] = useState({ name: "", assetType: "SqlDatabase", provider: "", resourceIdentifier: "" });
  const [selectedLinkedServiceIds, setSelectedLinkedServiceIds] = useState<string[]>([]);
  const [linkSelections, setLinkSelections] = useState<Record<string, string>>({});
  const [error, setError] = useState("");

  const load = async () => {
    const [assetList, serviceList] = await Promise.all([infraApi.list(), servicesApi.list()]);
    setAssets(assetList);
    setServices(serviceList);
    setLinkSelections((prev) => {
      const next = { ...prev };
      for (const asset of assetList as Asset[]) {
        next[asset.id] = next[asset.id] ?? asset.linkedServiceIds[0] ?? "";
      }
      return next;
    });
  };
  useEffect(() => { load(); }, []);

  const openCreate = () => {
    setEditing(null);
    setForm({ name: "", assetType: "SqlDatabase", provider: "", resourceIdentifier: "" });
    setSelectedLinkedServiceIds([]);
    setShowForm(true);
    setError("");
  };
  const openEdit = (a: Asset) => {
    setEditing(a);
    setForm({ name: a.name, assetType: a.assetType, provider: a.provider ?? "", resourceIdentifier: a.resourceIdentifier ?? "" });
    setSelectedLinkedServiceIds(a.linkedServiceIds);
    setShowForm(true);
    setError("");
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setError("");
    const payload = { ...form, assetType: ASSET_TYPES.indexOf(form.assetType) };
    try {
      let savedAssetId = editing?.id;
      let previousLinks = editing?.linkedServiceIds ?? [];

      if (editing)
      {
        await infraApi.update(editing.id, payload);
      }
      else
      {
        const created = await infraApi.create(payload);
        savedAssetId = created.id;
        previousLinks = [];
      }

      if (savedAssetId)
      {
        const toAdd = selectedLinkedServiceIds.filter((id) => !previousLinks.includes(id));
        const toRemove = previousLinks.filter((id) => !selectedLinkedServiceIds.includes(id));

        await Promise.all([
          ...toAdd.map((serviceId) => infraApi.link(savedAssetId as string, serviceId)),
          ...toRemove.map((serviceId) => infraApi.unlink(savedAssetId as string, serviceId))
        ]);
      }

      setShowForm(false); load();
    } catch { setError("Operation failed."); }
  };

  const del = async (id: string) => { if (!confirm("Delete?")) return; await infraApi.delete(id); load(); };
  const link = async (assetId: string) => {
    const serviceId = linkSelections[assetId];
    if (!serviceId) return;
    await infraApi.link(assetId, serviceId);
    await load();
  };

  const visibleAssets = selectedServiceFilter
    ? assets.filter((asset) => asset.linkedServiceIds.includes(selectedServiceFilter))
    : assets;

  const serviceName = (id: string) => services.find((s) => s.id === id)?.name ?? id;

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-semibold text-white">Infrastructure Inventory</h1>
          {selectedServiceFilter && (
            <p className="mt-1 text-xs text-cyan-300">
              Filtered by service: {serviceName(selectedServiceFilter)} {"("}
              <Link href="/dashboard/infrastructure" className="underline hover:text-cyan-200">clear</Link>
              {")"}
            </p>
          )}
        </div>
        <button onClick={openCreate} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500">+ New Asset</button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5 space-y-4">
          <h2 className="font-medium text-white">{editing ? "Edit Asset" : "Create Asset"}</h2>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            <div><label className="mb-1 block text-sm text-slate-300">Name *</label><input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="input" /></div>
            <div><label className="mb-1 block text-sm text-slate-300">Type</label>
              <select value={form.assetType} onChange={(e) => setForm({ ...form, assetType: e.target.value })} className="input">
                {ASSET_TYPES.map((t) => <option key={t}>{t}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Provider</label><input value={form.provider} onChange={(e) => setForm({ ...form, provider: e.target.value })} className="input" placeholder="e.g. Azure, AWS" /></div>
            <div><label className="mb-1 block text-sm text-slate-300">Resource ID</label><input value={form.resourceIdentifier} onChange={(e) => setForm({ ...form, resourceIdentifier: e.target.value })} className="input" /></div>
          </div>
          <div>
            <label className="mb-2 block text-sm text-slate-300">Connected Services</label>
            <div className="grid gap-2 sm:grid-cols-2">
              {services.map((service) => {
                const checked = selectedLinkedServiceIds.includes(service.id);
                return (
                  <label key={service.id} className="flex items-center gap-2 rounded border border-white/10 bg-white/5 px-3 py-2 text-sm text-slate-200">
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={(e) => {
                        setSelectedLinkedServiceIds((prev) =>
                          e.target.checked
                            ? [...prev, service.id]
                            : prev.filter((id) => id !== service.id));
                      }}
                    />
                    <span>{service.name}</span>
                  </label>
                );
              })}
            </div>
          </div>
          <div className="flex gap-3">
            <button type="submit" className="rounded-lg bg-cyan-600 px-4 py-2 text-sm text-white hover:bg-cyan-500">Save</button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-300">Cancel</button>
          </div>
        </form>
      )}

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left"><tr><th className="px-4 py-3 text-slate-400">Name</th><th className="px-4 py-3 text-slate-400">Type</th><th className="px-4 py-3 text-slate-400">Provider</th><th className="px-4 py-3 text-slate-400">Connected Services</th><th className="px-4 py-3 text-slate-400">Link to Service</th><th className="px-4 py-3"></th></tr></thead>
          <tbody>
            {visibleAssets.map((a) => (
              <tr key={a.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-white">{a.name}</td>
                <td className="px-4 py-3"><span className="rounded-full bg-purple-500/10 px-2 py-0.5 text-xs text-purple-300">{a.assetType}</span></td>
                <td className="px-4 py-3 text-slate-400">{a.provider ?? "—"}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-2">
                    {a.linkedServiceIds.length === 0 && <span className="text-xs text-slate-500">None</span>}
                    {a.linkedServiceIds.map((id) => (
                      <Link key={`${a.id}-${id}`} href={`/dashboard/infrastructure?serviceId=${id}`} className="rounded-full bg-cyan-500/10 px-2 py-0.5 text-xs text-cyan-300 hover:bg-cyan-500/20">
                        {serviceName(id)}
                      </Link>
                    ))}
                  </div>
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-1">
                    <select className="input !py-1 text-xs" value={linkSelections[a.id] ?? ""} onChange={(e) => setLinkSelections((prev) => ({ ...prev, [a.id]: e.target.value }))}>
                      <option value="">Select…</option>
                      {services.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                    </select>
                    <button type="button" onClick={() => link(a.id)} className="rounded bg-cyan-600/20 px-2 text-xs text-cyan-300 hover:bg-cyan-600/40">Link</button>
                  </div>
                </td>
                <td className="px-4 py-3 flex gap-2 justify-end">
                  <button onClick={() => openEdit(a)} className="text-xs text-cyan-400 hover:underline">Edit</button>
                  <button onClick={() => del(a.id)} className="text-xs text-red-400 hover:underline">Delete</button>
                </td>
              </tr>
            ))}
            {visibleAssets.length === 0 && <tr><td colSpan={6} className="px-4 py-6 text-center text-slate-500">No assets in this view.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
