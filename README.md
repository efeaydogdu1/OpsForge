# OpsForge

OpsForge is a production-grade internal developer platform built as a modular monolith with ASP.NET Core, PostgreSQL, Docker Compose, and React/Next.js.

## Current status

Week 1 foundation is in place:

- ASP.NET Core backend solution with PostgreSQL EF Core persistence
- JWT auth and refresh-token plumbing
- Teams CRUD foundation
- Next.js frontend landing shell
- Backend and frontend build validation passing

## Local development

Backend:

```powershell
cd backend
dotnet build OpsForge.sln
dotnet test OpsForge.sln
```

Frontend:

```powershell
cd frontend
npm run build
```

## Run locally with Docker

```powershell
docker compose up --build
```

Then open:

- Frontend: http://localhost:3000
- Backend API: http://localhost:8080/api/v1

Use the landing page buttons to go to `/register` for a new account or `/login` to sign in, then the protected dashboard is available under `/dashboard`.

## Architecture

- Modular monolith boundaries by domain module
- Clean Architecture layering
- CQRS via MediatR
- FluentValidation for command validation
- EF Core with PostgreSQL
- JWT access tokens and refresh tokens
- Audit logging module planned as a cross-cutting slice

