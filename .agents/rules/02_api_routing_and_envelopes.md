# 02 — API ROUTING, ENVELOPES & PAGINATION (USER SERVICE)

## 1. Route Conventions in UserService
* Collections: Lowercase plural nouns (`/api/v1/users`, `/api/v1/users/{userId}`, `/api/v1/admin/users`, `/api/v1/sellers`).
* Contextual Singleton Exceptions:
  * `/api/v1/me`: Current user profile actions (`GET /api/v1/me`, `PATCH /api/v1/me`).
  * `/api/v1/auth`: Authentication operations (`/api/v1/auth/login`, `/api/v1/auth/register`, `/api/v1/auth/otp/verify`).
* Prohibit singular nouns for collections (`/api/v1/user`, `/api/v1/seller`).
* One route attribute per action. Use single-line XML `<summary>`.

## 2. Pure DTO Return & Zero ResultFilter
* Controllers return pure DTOs:
  * `Ok(userProfileDto)`
  * `StatusCode(StatusCodes.Status201Created, createdUserDto)`
  * `NoContent()` (for delete/empty operations)
  * `Ok(pagedResultDto)` (for paginated lists)
* Zero ResultFilter: Do NOT apply or register `ResultFilter` or manual envelope wrappers in `User.Api`. Gateway's `ResponseEnvelopeMiddleware` handles envelope wrapping automatically.

## 3. Database Pagination Governance
* Pagination computed 100% via SQL: `.Skip((page - 1) * limit).Take(limit)` and `.CountAsync()`.
* Packaged in `PagedResultDto<T>` or `PagedData<T>` containing `items` and pagination metadata (`total`, `page`, `limit`, `totalPages`).
