---
name: controller-conventions
description: Canonical standards for ASP.NET Core thin controllers in UserService (port 8001). Covers boundary validation, User.Application shared request/response DTOs, English sentence-case error messages, identity extraction, and the reference controller blueprint.
---

# Controller Conventions & Standards in UserService

> Scope: UserService repository only. Never reference parent workspace paths, root documentation, or sibling service directories.

This skill defines the invariant structural and boundary rules for all REST API controllers in `User.Api`.

Controllers serve strictly as **HTTP Transport Boundary Adapters**. They bind incoming HTTP protocol details, extract verified identities, invoke Application feature use cases, and translate results into standardized HTTP responses. They must never contain business decisions, direct database queries, or manual error formatting.

---

## 1. When to Use

Consult and follow this skill whenever you:
- Create a new controller or add action methods in `User.Api/Controllers/Public/`.
- Review controller code changes for architectural consistency in UserService.
- Bind request parameters (`[FromRoute]`, `[FromQuery]`, `[FromBody]`).
- Design validation placement for incoming client payloads.
- Format outbound responses, pagination metadata, or status codes.

---

## 2. Request and Response DTO Placement (Application Layer Sharing)

To prevent duplicate wrapper classes and maintain clean domain boundaries:

- **Declare DTOs in User.Application**: All Request and Response records are declared directly in `User.Application/Services/{Feature}/{Feature}Requests.cs` and `{Feature}Responses.cs`.
- **Direct Consumption by Controller**: Controllers directly bind these Application-layer request records and return these Application-layer response records.
- **Prohibit Duplicate API DTOs**: Never create redundant DTOs in `User.Api` (e.g. prohibited: creating `ApiCreateUserRequest` in Api only to map into `CreateUserRequest` in Application).
- **Domain Entities Stay Private**: Never expose EF Core entities or domain aggregate roots (`User`, `Role`, `SellerRegistration`) through the controller. Only immutable DTO records and `PagedData<T>` leave the application boundary.

---

## 3. Validation Placement (Boundary Discipline)

Validation is concentrated exclusively at system boundaries:

- **Boundary Validation on Request Records**: Place declarative validation attributes from `System.ComponentModel.DataAnnotations` directly on Request DTO records (`[Required]`, `[EmailAddress]`, `[StringLength]`, `[Range]`, `[RegularExpression]`).
- **Automatic Model State Rejection**: Invalid inputs are intercepted automatically by ASP.NET Core model validation via `ApiValidationProblemFactory`, producing standardized RFC 9110 ProblemDetails with `errorCode = "VAL_INVALID_INPUT"`.
- **PROHIBIT Manual Validation in Controllers**: Never write ad-hoc validation checks inside controller action bodies (e.g. prohibited: `if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest(...);`).

---

## 4. Error Message Standards (Descriptive Sentence Case)

When domain exceptions, validation failures, or edge guards trigger errors:

- **100% English Language**: All error messages, failure details, and exception descriptions must be written in English.
- **Sentence Case Format**:
  - Capitalize ONLY the first letter of the sentence.
  - All subsequent words must be lowercase, except technical acronyms and identifiers (`ID`, `OTP`, `JWT`, `URL`, `UUID`, `Email`, `API`, `HTTP`).
  - Prohibit ALL-CAPS messages (e.g. prohibited: `"USER NOT FOUND"`, `"INVALID CREDENTIALS"`).
  - Prohibit Title Case (e.g. prohibited: `"User Account Not Found"`).
- **Contextual Precision**: The message must precisely describe what condition failed so any developer or client can diagnose the issue immediately.
  - Correct: `"Email is already registered in the system."`
  - Correct: `"Password must be at least 8 characters long and contain a digit."`
  - Correct: `"User account was not found for the specified identifier."`
- **Error Codes**: Always `SCREAMING_SNAKE_CASE` without arbitrary `ERR_` prefixes (`EMAIL_ALREADY_EXISTS`, `USER_NOT_FOUND`, `INVALID_CREDENTIALS`, `VAL_INVALID_INPUT`).

---

## 5. Canonical Reference Controller Blueprint

The following structural skeleton represents the standardized pattern for controllers in `User.Api`:

```csharp
namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common.Models;
using User.Application.Services.{Feature};

[ApiController]
[Route("api/v1/{plural-resources}")]
public sealed class {Feature}Controller : ControllerBase
{
    private readonly I{Feature}Service _service;
    private readonly ILogger<{Feature}Controller> _logger;

    public {Feature}Controller(
        I{Feature}Service service,
        ILogger<{Feature}Controller> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lists resources with pagination.</summary>
    [HttpGet]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedData<{Feature}Dto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        _logger.LogInformation("[REST {Feature}] Querying page: {Page}, size: {Size}", page, pageSize);
        var result = await _service.ListAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets a single resource by its unique identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<{Feature}Dto>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST {Feature}] Fetching by Id: '{Id}'", id);
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new resource from an Application-layer request record.</summary>
    [HttpPost]
    [RequireRole("admin", "seller", "buyer")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] Create{Feature}Request request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST {Feature}] Creating resource for UserId: '{UserId}'", userId);
        var result = await _service.CreateAsync(userId, request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Deletes a resource by its unique identifier.</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST {Feature}] Deleting resource Id: '{Id}'", id);
        await _service.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
```

---

## 6. Identity & Ownership Extraction Invariants

- **Extract Identity via Extension**: Always use `this.TryGetUserId(out var userId, out var authError)` from `User.Api.Extensions`.
- **Enforce Ownership via Extension**: For resources owned by a user, call `this.EnsureOwnership(targetUserId, out var ownershipError)` before executing mutating actions.
- **Never Trust Query/Route IDs for Current User**: Never bind `[FromQuery] Guid userId` or `[FromBody] Guid buyerId` when operating on authenticated user actions. Route `/api/v1/me` operates exclusively on the verified token subject.

---

## 7. HTTP Status & Return Conventions

- **GET Single Item**: Return `Task<ActionResult<T>>` with `Ok(dto)` (HTTP 200).
- **GET Paginated List**: Return `Task<ActionResult<PagedData<T>>>` with `Ok(pagedData)` (HTTP 200).
- **POST Creation**: Return `Task<IActionResult>` with `StatusCode(StatusCodes.Status201Created, dto)` (HTTP 201).
- **PUT / PATCH Update**: Return `Task<IActionResult>` with `Ok(dto)` (HTTP 200).
- **DELETE or Empty Operations**: Return `Task<IActionResult>` with `NoContent()` (HTTP 204, 0-byte payload).
- **No Manual Outer Envelopes**: Microservice controllers return pure payloads. Wrapping into `ApiResponse<T>` is performed by Gateway `ResponseEnvelopeMiddleware`.
- **Automatic Error Responses**: All unhandled domain exceptions bubble up to `GlobalExceptionHandlerMiddleware` to format RFC 9110 ProblemDetails.
