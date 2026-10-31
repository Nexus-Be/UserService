# 04 — AUDIT LOGGING & SECURITY TRAILS (USER SERVICE)

## 1. Mandatory Audit Logging for User Operations
Record audit events for the following critical operations:
* Password changes, password resets, account registration, email verification.
* Administrative user creation, updates, and soft deletion.
* Account ban (`BanUserAsync`) and unban actions (`PATCH /api/v1/admin/users/{userId}/ban`).
* Seller onboarding application submission, KYC document updates, review approvals and rejections (`PATCH /api/v1/sellers/registrations/{id}/review`).
* Role assignment and permission alterations.

## 2. Audit Persistence Standards
* Business entities inherit `AuditableEntity<Guid>` (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`).
* Audit rows are written into this service's own `AuditLogs` table through `_dbContext.AddAuditLog(...)` (`User.Application/Common/Extensions/AuditLogExtensions.cs`).
* The audit row is committed in the same database transaction as the business change.
* Never instantiate `new AuditLog { ... }` outside the extension. Never replace an audit row with `_logger.LogInformation`.
* Never call `LogService` over HTTP inside the business transaction. Rows start with `IsSynced = false`; a background sync may later forward them to `POST /api/v1/logs/batch` as a plain REST call.
* Audit rows are append-only. INSERT and SELECT only, never UPDATE or DELETE.
* Field reference: `ServiceName`, `Action`, `EntityName`, `EntityId`, `UserId`, `UserName`, `OldValues`, `NewValues`, `ChangedColumns`, `TraceId`, `IpAddress`, `UserAgent`, `CreatedAt`, `IsSynced`.
