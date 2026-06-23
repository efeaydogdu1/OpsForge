"use client";
import { useEffect, useState } from "react";
import { useAuth } from "@/context/AuthContext";
import { githubApi, userGitHubTokensApi } from "@/lib/api";

interface GitHubToken {
  id: string;
  name: string;
  tokenLastFour: string;
  isDefault: boolean;
  isActive: boolean;
  createdAtUtc: string;
  lastUsedAtUtc?: string;
}

export default function UserPage() {
  const { user } = useAuth();
  const [tokens, setTokens] = useState<GitHubToken[]>([]);
  const [name, setName] = useState("");
  const [token, setToken] = useState("");
  const [isDefault, setIsDefault] = useState(true);
  const [saving, setSaving] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [syncResult, setSyncResult] = useState<string>("");
  const [error, setError] = useState("");

  const load = async () => {
    setTokens(await userGitHubTokensApi.list());
  };

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      const nextTokens = await userGitHubTokensApi.list();
      if (!cancelled) {
        setTokens(nextTokens);
      }
    };

    void initialize();

    return () => {
      cancelled = true;
    };
  }, []);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError("");

    try {
      await userGitHubTokensApi.create({ name, token, isDefault });
      setName("");
      setToken("");
      setIsDefault(tokens.length === 0);
      await load();
    } catch {
      setError("Could not save GitHub token.");
    } finally {
      setSaving(false);
    }
  };

  const updateToken = async (next: GitHubToken) => {
    setError("");
    try {
      await userGitHubTokensApi.update(next.id, {
        name: next.name,
        isDefault: next.isDefault,
        isActive: next.isActive,
      });
      await load();
    } catch {
      setError("Could not update GitHub token.");
    }
  };

  const removeToken = async (id: string) => {
    if (!confirm("Delete this GitHub token?")) return;

    setError("");
    try {
      await userGitHubTokensApi.delete(id);
      await load();
    } catch {
      setError("Could not delete GitHub token.");
    }
  };

  const syncGitHubAccount = async () => {
    setSyncing(true);
    setError("");
    setSyncResult("");

    try {
      const result = await githubApi.syncAccount();
      setSyncResult(
        `Imported ${result.repositoriesImported} repos, ${result.servicesCreated} new services, ${result.environmentsImported} environments, ${result.deploymentsImported} CI/CD processes, ${result.issuesImported} issues.`
      );
      await load();
    } catch {
      setError("Could not sync GitHub account. Check that the token is active and has repository access.");
    } finally {
      setSyncing(false);
    }
  };

  return (
    <div className="max-w-5xl">
      <div className="mb-6">
        <h1 className="text-2xl font-semibold text-white">User</h1>
        <p className="mt-1 text-sm text-slate-400">{user?.displayName} / {user?.email}</p>
      </div>

      <section className="mb-6 rounded-xl border border-white/10 bg-white/5 p-5">
        <h2 className="text-lg font-medium text-white">GitHub Tokens</h2>
        <p className="mt-1 text-sm text-slate-400">Tokens are encrypted after saving and used for repository preview, link, and sync operations.</p>

        <form onSubmit={submit} className="mt-5 grid gap-4 lg:grid-cols-[0.7fr_1fr_auto] lg:items-end">
          <div>
            <label className="mb-1 block text-sm text-slate-300">Name *</label>
            <input required value={name} onChange={(e) => setName(e.target.value)} className="input" placeholder="Personal GitHub" />
          </div>
          <div>
            <label className="mb-1 block text-sm text-slate-300">Token *</label>
            <input required type="password" value={token} onChange={(e) => setToken(e.target.value)} className="input" placeholder="ghp_..." autoComplete="off" />
          </div>
          <label className="flex items-center gap-2 rounded-lg border border-white/10 px-3 py-2 text-sm text-slate-300">
            <input type="checkbox" checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} />
            Default
          </label>
          <div className="lg:col-span-3">
            <button disabled={saving} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500 disabled:opacity-60">
              {saving ? "Saving..." : "Add Token"}
            </button>
          </div>
        </form>

        {error && <p className="mt-4 rounded-lg bg-red-500/10 px-4 py-2 text-sm text-red-300">{error}</p>}
      </section>

      <section className="mb-6 rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-lg font-medium text-white">GitHub Account Sync</h2>
            <p className="mt-1 text-sm text-slate-400">Import accessible repositories into Services, branch/environment names into Environments, and GitHub deployment activity into CI/CD Processes.</p>
          </div>
          <button onClick={syncGitHubAccount} disabled={syncing || tokens.length === 0} className="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500 disabled:opacity-60">
            {syncing ? "Syncing..." : "Sync GitHub"}
          </button>
        </div>
        {syncResult && <p className="mt-4 rounded-lg bg-green-500/10 px-4 py-2 text-sm text-green-300">{syncResult}</p>}
      </section>

      <div className="rounded-xl border border-white/10 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-white/5 text-left">
            <tr>
              <th className="px-4 py-3 text-slate-400">Name</th>
              <th className="px-4 py-3 text-slate-400">Token</th>
              <th className="px-4 py-3 text-slate-400">Status</th>
              <th className="px-4 py-3 text-slate-400">Last Used</th>
              <th className="px-4 py-3"></th>
            </tr>
          </thead>
          <tbody>
            {tokens.map((item) => (
              <tr key={item.id} className="border-t border-white/5 hover:bg-white/5">
                <td className="px-4 py-3 text-white">{item.name}</td>
                <td className="px-4 py-3 text-slate-300">**** {item.tokenLastFour}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-2">
                    <span className={`rounded-full px-2 py-0.5 text-xs ${item.isActive ? "bg-green-500/10 text-green-300" : "bg-slate-500/10 text-slate-400"}`}>{item.isActive ? "Active" : "Inactive"}</span>
                    {item.isDefault && <span className="rounded-full bg-cyan-500/10 px-2 py-0.5 text-xs text-cyan-300">Default</span>}
                  </div>
                </td>
                <td className="px-4 py-3 text-slate-400">{item.lastUsedAtUtc ? new Date(item.lastUsedAtUtc).toLocaleString() : "Never"}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    {!item.isDefault && item.isActive && (
                      <button onClick={() => updateToken({ ...item, isDefault: true })} className="text-xs text-cyan-400 hover:underline">Make Default</button>
                    )}
                    <button onClick={() => updateToken({ ...item, isActive: !item.isActive, isDefault: item.isActive ? false : item.isDefault })} className="text-xs text-amber-400 hover:underline">
                      {item.isActive ? "Disable" : "Enable"}
                    </button>
                    <button onClick={() => removeToken(item.id)} className="text-xs text-red-400 hover:underline">Delete</button>
                  </div>
                </td>
              </tr>
            ))}
            {tokens.length === 0 && <tr><td colSpan={5} className="px-4 py-6 text-center text-slate-500">No GitHub tokens saved.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
