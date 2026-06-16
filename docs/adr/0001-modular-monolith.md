# ADR 0001: Modular Monolith

## Status
Accepted

## Context
OpsForge needs domain separation without the operational cost of microservices in Week 1.

## Decision
Use a modular monolith with separate domain modules and one ASP.NET Core API host.

## Consequences
- Easier local development and deployment
- Clear boundaries for future extraction
- Shared runtime and database require strong module discipline
