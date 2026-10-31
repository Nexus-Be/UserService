# 03 — SECURITY HEADERS, INTERNAL API & IDENTITY (USER SERVICE)

## 1. Public Controllers And Temporary Internal Disable
* All controllers live under `User.Api/Controllers/Public/`. There is no `Controllers/Internal/` folder.
* Internal API security is temporarily disabled. `InternalServiceAuthMiddleware` registration is commented; no route requires `X-Internal-Service-Key`.
* Inter-service calls use plain REST routes: `POST /api/v1/notifications/email/otp`, `POST /api/v1/wallets/checkout/direct`, `POST /api/v1/inventories/initialize`, `POST /api/v1/logs/batch`.
* Re-enabling the old model requires explicit approval and restoring middleware, handler, Gateway blocking, and docs together.

## 3. Trusted Identity Extraction & Ownership Guard
* Never trust client query/route parameters for identity (`userId`).
* Use extension methods in `User.Api.Extensions.HttpContextExtensions`:
  ```csharp
  if (!this.TryGetUserId(out var currentUserId, out var authError))
      return authError;

  if (!this.EnsureOwnership(targetUserId, out var forbidError))
      return forbidError;
  ```
* Admin endpoints require `EnsureAdmin` or role check `admin` / `staff`.
