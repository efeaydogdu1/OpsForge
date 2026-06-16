# OpsForge Architecture

OpsForge uses a modular monolith structure with a single ASP.NET Core deployment unit and a React/Next.js frontend.

## Principles

- Domain-driven module separation
- Clean Architecture boundaries
- CQRS with MediatR
- FluentValidation for request validation
- PostgreSQL via EF Core
- JWT authentication with refresh tokens
- Audit logging for security and traceability

## Week 1 modules

- Identity
- Teams
- Services
- Environments
- Infrastructure Inventory
- Deployments
- Audit Logging
