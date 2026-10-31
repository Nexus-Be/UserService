# 05 — ERROR HANDLING & ENUM BOUNDARY (USER SERVICE)

## 1. 100% English & Sentence Case
* All error messages must be 100% English sentence case.
* Capitalize only the first letter. Keep technical acronyms uppercase (`ID`, `OTP`, `JWT`, `Email`).
* Examples: `"Email is already registered in the system."`, `"Password must be at least 8 characters long."`, `"User account not found."`

## 2. SCREAMING_SNAKE_CASE Error Codes
* Error codes must be uppercase with underscores. No `ERR_` prefixes.
* Use `VAL_` prefix for validation errors.
* Examples: `EMAIL_ALREADY_EXISTS`, `OTP_EXPIRED`, `OTP_INVALID`, `USER_NOT_FOUND`, `VAL_INVALID_INPUT`, `VAL_INVALID_USER_ID`, `UNAUTHORIZED`, `FORBIDDEN`.

## 3. RFC 9110 ProblemDetails Structure
* Handled by `GlobalExceptionHandlerMiddleware.cs`: returns `application/problem+json` containing `type`, `title`, `status`, `errorCode`, `detail`, `traceId`.

## 4. Enum Persistence & Boundary Serialization
* In PostgreSQL (`usermanagement_db`): 100% stored as `integer` (int32). Prohibit `HasConversion<string>()`.
* Enums: `UserStatus` (`Active = 1`, `PendingVerification = 2`, `Banned = 3`), `SellerVerificationStatus` (`Pending = 0`, `Approved = 1`, `Rejected = 2`).
* REST Outbound: 100% serialized as `PascalCase` strings (`"Active"`, `"PendingVerification"`, `"Approved"`).
* REST Inbound: Handled via `PascalCaseJsonStringEnumConverter(allowIntegerValues: false)`.
