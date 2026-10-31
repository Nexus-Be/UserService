# 06 — DATABASE PERSISTENCE & MIGRATIONS (USER SERVICE)

## 1. Database Isolation
* Dedicated database: `usermanagement_db` on host port `5433`.
* Primary key: 100% `Guid` for entities (`Users`, `Roles`, `Permissions`, `SellerRegistrations`).
* Prohibit direct queries or joins to databases of other services.

## 2. Entity Configuration & Soft Delete
* Every entity declares `IsDeleted` and `DeletedAt`. Enforce query filter: `builder.HasQueryFilter(x => !x.IsDeleted)`.
* Filtered index for unique fields:
  ```csharp
  builder.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
  builder.HasIndex(x => x.Username).IsUnique().HasFilter("\"Username\" IS NOT NULL");
  ```
* Seed Data: Fixed deterministic GUIDs (`Guid.Parse(...)`). Password hashed with BCrypt.

## 3. Migration Safety
* No destructive migrations (`DROP TABLE`, `DROP COLUMN`) without explicit authorization.
* Startup safe auto-migration: `db.Database.Migrate()` wrapped in `try-catch` checking `db.Database.IsRelational()`.
