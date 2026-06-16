import Link from "next/link";

const modules = ["Identity", "Teams", "Services", "Environments", "Infrastructure Inventory", "Deployments", "Audit Logging"];

export default function Home() {
  return (
    <main className="min-h-screen bg-[radial-gradient(circle_at_top_left,_#1e293b_0,_#0f172a_35%,_#020617_100%)] text-slate-100">
      <div className="mx-auto flex min-h-screen w-full max-w-7xl flex-col justify-between px-6 py-8 lg:px-10">
        <header className="flex items-center justify-between border-b border-white/10 pb-6">
          <div>
            <p className="text-sm uppercase tracking-[0.35em] text-cyan-300/80">OpsForge</p>
            <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white sm:text-4xl">Internal Developer Platform</h1>
          </div>
          <div className="rounded-full border border-cyan-400/30 bg-cyan-400/10 px-4 py-2 text-sm text-cyan-200">Week 1 foundation live</div>
        </header>

        <section className="grid gap-8 py-10 lg:grid-cols-[1.3fr_0.9fr] lg:items-end">
          <div className="space-y-6">
            <p className="max-w-2xl text-lg leading-8 text-slate-300">
              Modular monolith backend, PostgreSQL persistence, authenticated platform workflows, and a Next.js UI built to grow into full team, service, deployment, and audit operations.
            </p>
            <div className="flex flex-wrap gap-3">
              <Link href="/login" className="rounded-full bg-cyan-400 px-5 py-3 text-sm font-medium text-slate-950 transition hover:bg-cyan-300">
                Open login
              </Link>
              <Link href="/register" className="rounded-full border border-cyan-400/30 bg-cyan-400/10 px-5 py-3 text-sm font-medium text-cyan-200 transition hover:bg-cyan-400/20">
                Create account
              </Link>
              {modules.map((module) => (
                <span key={module} className="rounded-full border border-white/10 bg-white/5 px-4 py-2 text-sm text-slate-200">{module}</span>
              ))}
            </div>
          </div>

          <div className="rounded-3xl border border-white/10 bg-white/5 p-6 shadow-2xl shadow-cyan-950/20 backdrop-blur">
            <h2 className="text-sm uppercase tracking-[0.3em] text-cyan-300">Next steps</h2>
            <ul className="mt-4 space-y-3 text-sm leading-6 text-slate-300">
              <li>• Connect login and refresh flows to the backend API.</li>
              <li>• Build Teams and Services CRUD screens.</li>
              <li>• Add infrastructure inventory and deployment tracking pages.</li>
              <li>• Wire audit history into the protected shell.</li>
            </ul>
          </div>
        </section>

        <footer className="flex flex-col gap-2 border-t border-white/10 pt-6 text-sm text-slate-400 sm:flex-row sm:items-center sm:justify-between">
          <span>ASP.NET Core + PostgreSQL + Docker Compose + React/Next.js</span>
          <span>Clean Architecture, CQRS, FluentValidation, Testcontainers</span>
        </footer>
      </div>
    </main>
  );
}
