# ADR 0002: JWT Access Tokens and Refresh Cookies

## Status
Accepted

## Context
The platform needs stateless API authentication with safe token renewal.

## Decision
Use short-lived JWT access tokens and long-lived refresh tokens stored in an HttpOnly secure cookie in the UI flow.

## Consequences
- Access tokens remain small and short-lived
- Refresh flow avoids exposing long-lived secrets to browser JavaScript
- Server must rotate and revoke refresh tokens carefully
