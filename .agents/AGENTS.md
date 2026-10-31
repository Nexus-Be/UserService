# USER SERVICE — AI AGENT MISSION PROTOCOL & RULES

> **Service:** UserService (Port 8001, Database `usermanagement_db:5433`)
> **Scoping:** 100% Inward-Only. Never reference outward to root or sibling services.

## Local Governance Rules
Every AI Coding Agent working inside UserService must strictly comply with the local rules in `.agents/rules/`:
1. [01 Clean Architecture Layers](rules/01_clean_architecture_layers.md): 4-layer structure, Feature Co-location, No-Repository over EF Core.
2. [02 API Routing & Envelopes](rules/02_api_routing_and_envelopes.md): 100% Plural route convention, pure DTO return, zero ResultFilter.
3. [03 Security Headers & Internal API](rules/03_security_headers_and_internal.md): Public controllers; internal key check temporarily disabled, ownership guards.
4. [04 Audit & Security Logging](rules/04_audit_and_event_logging.md): AuditableEntity tracking, mutating operation audit logging.
5. [05 Error Handling & Enums](rules/05_error_handling_and_enums.md): 100% English sentence-case messages, SCREAMING_SNAKE_CASE error codes, PascalCase JSON enum serialization.
6. [06 Database & Migrations](rules/06_database_and_migrations.md): Database isolation, PostgreSQL integer enums, soft delete, deterministic seed GUIDs.

## Endpoint Workflow
Before adding or changing an endpoint, follow `.agents/skills/new-feature-endpoint/SKILL.md`.

## Verification Checklist
- `dotnet build User.sln -c Release` must succeed with 0 errors.
- `dotnet test User.sln -c Release` must pass 100%.
