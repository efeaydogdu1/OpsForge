"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { githubApi, servicesApi, teamsApi } from "@/lib/api";

interface Service { id: string; name: string; description?: string; ownerTeamId?: string; criticality: string; repositoryUrl?: string; isDeleted: boolean; }
interface Team { id: string; name: string; }
interface GitHubRepositoryMetadata { owner: string; name: string; url: string; defaultBranch?: string; description?: string; visibility?: string; primaryLanguage?: string; latestCommitSha?: string; latestCommitDateUtc?: string; latestCommitMessage?: string; }
interface GitHubSyncRun { id: string; serviceId: string; startedAtUtc: string; completedAtUtc: string; isSuccess: boolean; errorMessage?: string; owner: string; name: string; url: string; defaultBranch?: string; description?: string; visibility?: string; primaryLanguage?: string; latestCommitSha?: string; latestCommitDateUtc?: string; latestCommitMessage?: string; }

const CRITICALITIES = ["Low", "Medium", "High", "Critical"];

export default function ServicesPage() {
  const searchParams = useSearchParams();
  const selectedTeamFilter = searchParams.get("teamId") ?? "";
  const selectedServiceFilter = searchParams.get("serviceId") ?? "";
  const [services, setServices] = useState<Service[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [healthStatus, setHealthStatus] = useState<Record<string, { status: string; checkedAtUtc?: string; message?: string; statusCode?: number }>>({});
  const [checkingHealth, setCheckingHealth] = useState<Record<string, boolean>>({});
  const [selectedGitHubService, setSelectedGitHubService] = useState<Service | null>(null);
  const [githubRepositoryUrl, setGitHubRepositoryUrl] = useState("");
  const [githubPreview, setGitHubPreview] = useState<GitHubRepositoryMetadata | null>(null);
  const [githubSyncRuns, setGitHubSyncRuns] = useState<GitHubSyncRun[]>([]);
  const [githubError, setGitHubError] = useState("");
  const [previewingGitHub, setPreviewingGitHub] = useState(false);
  const [linkingGitHub, setLinkingGitHub] = useState(false);
  const [syncingGitHub, setSyncingGitHub] = useState(false);
  const [loadingGitHubHistory, setLoadingGitHubHistory] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Service | null>(null);
  const [form, setForm] = useState({ name: "", description: "", ownerTeamId: "", criticality: "Medium", repositoryUrl: "" });
  const [error, setError] = useState("");

  const load = async () => {
    const [nextServices, nextTeams] = await Promise.all([servicesApi.list(), teamsApi.list()]);
    setServices(nextServices);
    setTeams(nextTeams);
  };

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      const [nextServices, nextTeams] = await Promise.all([servicesApi.list(), teamsApi.list()]);
      if (cancelled) {
        return;
      }

      setServices(nextServices);
      setTeams(nextTeams);
    };

    void initialize();

    return () => {
      cancelled = true;
    };
  }, []);

  const openCreate = () => { setEditing(null); setForm({ name: "", description: "", ownerTeamId: "", criticality: "Medium", repositoryUrl: "" }); setShowForm(true); setError(""); };
  const openEdit = (s: Service) => { setEditing(s); setForm({ name: s.name, description: s.description ?? "", ownerTeamId: s.ownerTeamId ?? "", criticality: s.criticality, repositoryUrl: s.repositoryUrl ?? "" }); setShowForm(true); setError(""); };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    const payload = { ...form, criticality: CRITICALITIES.indexOf(form.criticality), ownerTeamId: form.ownerTeamId || null };
    try {
      if (editing) await servicesApi.update(editing.id, payload);
      else await servicesApi.create(payload);
      setShowForm(false); load();
    } catch { setError("Operation failed."); }
  };

  const del = async (id: string) => { if (!confirm("Delete?")) return; await servicesApi.delete(id); load(); };

  const loadGitHubHistory = async (serviceId: string) => {
    setLoadingGitHubHistory(true);
    try {
      const runs = await githubApi.listSyncRuns(serviceId);
      setGitHubSyncRuns(runs);
    } catch {
      setGitHubSyncRuns([]);
    } finally {
      setLoadingGitHubHistory(false);
    }
  };

  const openGitHubPanel = async (service: Service) => {
    setSelectedGitHubService(service);
    setGitHubRepositoryUrl(service.repositoryUrl ?? "");
    setGitHubPreview(null);
    setGitHubError("");
    await loadGitHubHistory(service.id);
  };

  const previewGitHubRepository = async () => {
    if (!githubRepositoryUrl.trim()) {
      setGitHubError("Repository URL is required.");
      return;
    }

    setPreviewingGitHub(true);
    setGitHubError("");

    try {
      const preview = await githubApi.preview(githubRepositoryUrl.trim());
      setGitHubPreview(preview);
    } catch {
      setGitHubPreview(null);
      setGitHubError("Preview failed. Check the GitHub URL and token configuration.");
    } finally {
      setPreviewingGitHub(false);
    }
  };

  const linkGitHubRepository = async () => {
    if (!selectedGitHubService) {
      return;
    }

    if (!githubRepositoryUrl.trim()) {
      setGitHubError("Repository URL is required.");
      return;
    }

    setLinkingGitHub(true);
    setGitHubError("");

    try {
      const linked = await githubApi.link(selectedGitHubService.id, githubRepositoryUrl.trim());
      setGitHubPreview(linked);
      await load();
      const updatedService = {
        ...selectedGitHubService,
        repositoryUrl: linked.url,
      };
      setSelectedGitHubService(updatedService);
      setGitHubRepositoryUrl(linked.url);
      await loadGitHubHistory(selectedGitHubService.id);
    } catch {
      setGitHubError("Link failed. Preview the repository first or verify access.");
    } finally {
      setLinkingGitHub(false);
    }
  };

  const syncGitHubRepository = async () => {
    if (!selectedGitHubService) {
      return;
    }

    setSyncingGitHub(true);
    setGitHubError("");

    try {
      const run = await githubApi.sync(selectedGitHubService.id);
      setGitHubPreview({
        owner: run.owner,
        name: run.name,
        url: run.url,
        defaultBranch: run.defaultBranch,
        description: run.description,
        visibility: run.visibility,
        primaryLanguage: run.primaryLanguage,
        latestCommitSha: run.latestCommitSha,
        latestCommitDateUtc: run.latestCommitDateUtc,
        latestCommitMessage: run.latestCommitMessage,
      });
      await load();
      await loadGitHubHistory(selectedGitHubService.id);
    } catch {
      setGitHubError("Sync failed. Link a repository first and confirm backend GitHub access.");
    } finally {
      setSyncingGitHub(false);
    }
  };

  const checkHealth = async (service: Service) => {
    setCheckingHealth((prev) => ({ ...prev, [service.id]: true }));
    try {
      const result = await servicesApi.healthCheck(service.id);
      setHealthStatus((prev) => ({ ...prev, [service.id]: result }));
    } catch {
      setHealthStatus((prev) => ({ ...prev, [service.id]: { status: "unreachable", message: "Health check request failed." } }));
    } finally {
      setCheckingHealth((prev) => ({ ...prev, [service.id]: false }));
    }
  };

  const teamName = (id?: string) => teams.find((t) => t.id === id)?.name ?? "—";
  const visibleServices = services
    .filter((s) => !selectedTeamFilter || s.ownerTeamId === selectedTeamFilter)
    .filter((s) => !selectedServiceFilter || s.id === selectedServiceFilter);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-semibold text-white">Services</h1>
          {(selectedTeamFilter || selectedServiceFilter) && (
            <p className="mt-1 text-xs text-cyan-300">
              Filtered view {"("}
              <Link href="/dashboard/services" className="underline hover:text-cyan-200">clear</Link>
              {")"}
            </p>
          )}
        </div>
        <button onClick={openCreate} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500">+ New Service</button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5 space-y-4">
          <h2 className="font-medium text-white">{editing ? "Edit Service" : "Create Service"}</h2>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            <div><label className="mb-1 block text-sm text-slate-300">Name *</label><input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="input" /></div>
            <div><label className="mb-1 block text-sm text-slate-300">Owner Team</label>
              <select value={form.ownerTeamId} onChange={(e) => setForm({ ...form, ownerTeamId: e.target.value })} className="input">
                <option value="">None</option>
                {teams.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Criticality</label>
              <select value={form.criticality} onChange={(e) => setForm({ ...form, criticality: e.target.value })} className="input">
                {CRITICALITIES.map((c) => <option key={c}>{c}</option>)}
              </select>
            </div>
            <div><label className="mb-1 block text-sm text-slate-300">Repository URL</label><input value={form.repositoryUrl} onChange={(e) => setForm({ ...form, repositoryUrl: e.target.value })} className="input" /></div>
          </div>
          <div><label className="mb-1 block text-sm text-slate-300">Description</label><input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className="input" /></div>
          <div className="flex gap-3">
            <button type="submit" className="rounded-lg bg-cyan-600 px-4 py-2 text-sm text-white hover:bg-cyan-500">Save</button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-300 hover:text-white">Cancel</button>
          </div>
        </form>
      )}

      {selectedGitHubService && (
        <section className="mb-6 rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-5 space-y-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <p className="text-xs uppercase tracking-[0.25em] text-cyan-300">GitHub Integration</p>
              <h2 className="mt-1 text-xl font-semibold text-white">{selectedGitHubService.name}</h2>
              <p className="mt-1 text-sm text-slate-400">Preview repository metadata, link the repo to this service, trigger sync, and inspect sync history.</p>
            </div>
            <button
              type="button"
              onClick={() => {
                setSelectedGitHubService(null);
                setGitHubPreview(null);
                setGitHubSyncRuns([]);
                setGitHubError("");
              }}
              className="rounded-lg border border-white/10 px-3 py-2 text-sm text-slate-300 hover:text-white"
            >
              Close
            </button>
          </div>

          <div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
            <div className="space-y-4">
              <div>
                <label className="mb-1 block text-sm text-slate-300">GitHub Repository URL</label>
                <input
                  value={githubRepositoryUrl}
                  onChange={(e) => setGitHubRepositoryUrl(e.target.value)}
                  className="input"
                  placeholder="https://github.com/org/repo"
                />
              </div>

              <div className="flex flex-wrap gap-3">
                <button type="button" onClick={previewGitHubRepository} disabled={previewingGitHub} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500 disabled:opacity-60">
                  {previewingGitHub ? "Previewing..." : "Preview"}
                </button>
                <button type="button" onClick={linkGitHubRepository} disabled={linkingGitHub} className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-60">
                  {linkingGitHub ? "Linking..." : "Link Repository"}
                </button>
                <button type="button" onClick={syncGitHubRepository} disabled={syncingGitHub} className="rounded-lg bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-500 disabled:opacity-60">
                  {syncingGitHub ? "Syncing..." : "Sync Now"}
                </button>
              </div>

              {githubError && <p className="rounded-lg bg-red-500/10 px-4 py-2 text-sm text-red-300">{githubError}</p>}

              {githubPreview && (
                <div className="rounded-xl border border-white/10 bg-slate-950/40 p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-lg font-medium text-white">{githubPreview.owner}/{githubPreview.name}</p>
                    {githubPreview.visibility && <span className="rounded-full bg-white/5 px-2 py-0.5 text-xs text-slate-300">{githubPreview.visibility}</span>}
                    {githubPreview.primaryLanguage && <span className="rounded-full bg-cyan-500/10 px-2 py-0.5 text-xs text-cyan-300">{githubPreview.primaryLanguage}</span>}
                  </div>
                  <a href={githubPreview.url} target="_blank" rel="noreferrer" className="mt-2 inline-block text-sm text-cyan-300 hover:underline">
                    {githubPreview.url}
                  </a>
                  {githubPreview.description && <p className="mt-3 text-sm text-slate-300">{githubPreview.description}</p>}
                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    <div>
                      <p className="text-xs uppercase tracking-wide text-slate-500">Default Branch</p>
                      <p className="mt-1 text-sm text-white">{githubPreview.defaultBranch ?? "—"}</p>
                    </div>
                    <div>
                      <p className="text-xs uppercase tracking-wide text-slate-500">Latest Commit</p>
                      <p className="mt-1 text-sm text-white">{githubPreview.latestCommitSha ? githubPreview.latestCommitSha.slice(0, 10) : "—"}</p>
                    </div>
                  </div>
                  {(githubPreview.latestCommitMessage || githubPreview.latestCommitDateUtc) && (
                    <div className="mt-4 rounded-lg bg-white/5 p-3">
                      <p className="text-xs uppercase tracking-wide text-slate-500">Latest Commit Details</p>
                      <p className="mt-1 text-sm text-slate-200">{githubPreview.latestCommitMessage ?? "No message provided."}</p>
                      {githubPreview.latestCommitDateUtc && <p className="mt-2 text-xs text-slate-400">{new Date(githubPreview.latestCommitDateUtc).toLocaleString()}</p>}
                    </div>
                  )}
                </div>
              )}
            </div>

            <div className="rounded-xl border border-white/10 bg-slate-950/30 p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h3 className="text-sm font-medium text-white">Sync History</h3>
                  <p className="mt-1 text-xs text-slate-400">Most recent GitHub sync runs for this service.</p>
                </div>
                <button type="button" onClick={() => loadGitHubHistory(selectedGitHubService.id)} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:text-white">
                  Refresh
                </button>
              </div>

              <div className="mt-4 space-y-3">
                {loadingGitHubHistory && <p className="text-sm text-slate-400">Loading sync history...</p>}
                {!loadingGitHubHistory && githubSyncRuns.length === 0 && <p className="text-sm text-slate-500">No sync runs yet.</p>}
                {!loadingGitHubHistory && githubSyncRuns.map((run) => (
                  <div key={run.id} className="rounded-lg border border-white/10 bg-white/5 p-3">
                    <div className="flex items-center justify-between gap-3">
                      <span className={`rounded-full px-2 py-0.5 text-xs ${run.isSuccess ? "bg-green-500/10 text-green-300" : "bg-red-500/10 text-red-300"}`}>
                        {run.isSuccess ? "Success" : "Failed"}
                      </span>
                      <span className="text-xs text-slate-400">{new Date(run.completedAtUtc).toLocaleString()}</span>
                    </div>
                    <p className="mt-2 text-sm text-white">{run.owner}/{run.name}</p>
                    {run.latestCommitSha && <p className="mt-1 text-xs text-slate-400">Commit {run.latestCommitSha.slice(0, 10)}</p>}
                    {run.latestCommitMessage && <p className="mt-2 text-xs text-slate-300">{run.latestCommitMessage}</p>}
                    {run.errorMessage && <p className="mt-2 text-xs text-red-300">{run.errorMessage}</p>}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>
      )}

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left"><tr><th className="px-4 py-3 text-slate-400">Name</th><th className="px-4 py-3 text-slate-400">Owner Team</th><th className="px-4 py-3 text-slate-400">Criticality</th><th className="px-4 py-3 text-slate-400">Repository</th><th className="px-4 py-3 text-slate-400">Status</th><th className="px-4 py-3 text-slate-400">Health</th><th className="px-4 py-3"></th></tr></thead>
          <tbody>
            {visibleServices.map((s) => (
              <tr key={s.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-white">
                  <Link href={`/dashboard/infrastructure?serviceId=${s.id}`} className="hover:text-cyan-300 hover:underline">{s.name}</Link>
                </td>
                <td className="px-4 py-3 text-slate-300">
                  {s.ownerTeamId ? (
                    <Link href={`/dashboard/services?teamId=${s.ownerTeamId}`} className="hover:text-cyan-300 hover:underline">{teamName(s.ownerTeamId)}</Link>
                  ) : "—"}
                </td>
                <td className="px-4 py-3"><span className="rounded-full bg-amber-500/10 px-2 py-0.5 text-xs text-amber-300">{s.criticality}</span></td>
                <td className="px-4 py-3 text-slate-300">
                  {s.repositoryUrl ? (
                    <a href={s.repositoryUrl} target="_blank" rel="noreferrer" className="text-cyan-300 hover:underline">
                      {s.repositoryUrl.replace("https://github.com/", "")}
                    </a>
                  ) : (
                    <span className="text-slate-500">Not linked</span>
                  )}
                </td>
                <td className="px-4 py-3"><span className={`rounded-full px-2 py-0.5 text-xs ${s.isDeleted ? "bg-red-500/10 text-red-400" : "bg-green-500/10 text-green-400"}`}>{s.isDeleted ? "Deleted" : "Active"}</span></td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <button type="button" onClick={() => checkHealth(s)} disabled={checkingHealth[s.id]} className="rounded bg-cyan-600/20 px-2 py-1 text-xs text-cyan-300 hover:bg-cyan-600/40 disabled:opacity-60">
                      {checkingHealth[s.id] ? "Checking..." : "Health Check"}
                    </button>
                    {healthStatus[s.id] && (
                      <span className={`rounded-full px-2 py-0.5 text-xs ${healthStatus[s.id].status === "healthy" ? "bg-green-500/10 text-green-300" : healthStatus[s.id].status === "unhealthy" ? "bg-amber-500/10 text-amber-300" : "bg-red-500/10 text-red-300"}`}>
                        {healthStatus[s.id].status}
                        {healthStatus[s.id].statusCode ? ` (${healthStatus[s.id].statusCode})` : ""}
                      </span>
                    )}
                  </div>
                </td>
                <td className="px-4 py-3 flex gap-2 justify-end">
                  <button type="button" onClick={() => openGitHubPanel(s)} className="text-xs text-emerald-400 hover:underline">GitHub</button>
                  <button onClick={() => openEdit(s)} className="text-xs text-cyan-400 hover:underline">Edit</button>
                  <button onClick={() => del(s.id)} className="text-xs text-red-400 hover:underline">Delete</button>
                </td>
              </tr>
            ))}
            {visibleServices.length === 0 && <tr><td colSpan={7} className="px-4 py-6 text-center text-slate-500">No services in this view.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
