# 01 — CLEAN ARCHITECTURE & BOUNDED CONTEXT (USER SERVICE)

## 1. Bounded Context & Port Allocation
* Bounded Context: User authentication, JWT issuance, profile management, seller onboarding, administrative user governance.
* Service Port: `8001` (HTTP REST API).
* Database: PostgreSQL `usermanagement_db` on port `5433`.

## 2. 4-Layer Clean Architecture
* `User.Domain`: Entities (`User`, `Role`, `Permission`, `SellerRegistration`), Enums (`UserStatus`, `SellerVerificationStatus`), Exceptions (`DomainException`, `BusinessRuleValidationException`).
* `User.Application`: Use cases in `Services/{Feature}/`, interface `IUserDbContext`, request/response records, typed client interfaces (`Interfaces/IEmailNotificationClient.cs`).
* `User.Infrastructure`: `AppDbContext`, EF Core configurations, migrations, BCrypt hasher, JWT generator, and typed REST client `EmailNotificationClient`.
* `User.Api`: Thin controllers in `Controllers/Public/`, middlewares, extensions, composition root (`Program.cs`).

## 3. Feature Co-Location (Mandatory)
All use case components for a business feature must be located together inside `User.Application/Services/{Feature}/`:
* `Services/Auth/`: `IAuthService.cs`, `AuthService.cs`, `AuthRequests.cs`, `AuthResponses.cs`, `AuthDtos.cs`.
* `Services/Profile/`: `IProfileService.cs`, `ProfileService.cs`, `ProfileRequests.cs`, `ProfileResponses.cs`, `ProfileDtos.cs`.
* `Services/Seller/`: `ISellerService.cs`, `SellerService.cs`, `SellerRequests.cs`, `SellerResponses.cs`, `SellerDtos.cs`.
* `Services/Admin/`: `IAdminUserService.cs`, `AdminUserService.cs`, `AdminUserRequests.cs`, `AdminUserResponses.cs`, `AdminUserDtos.cs`.

## 4. Thin 1-to-1 Controller Pairing
Each feature service pairs 1-to-1 with a thin controller at `User.Api/Controllers/Public/{Feature}Controller.cs`:
* `AuthController.cs` -> injects `IAuthService`
* `UsersController.cs` -> injects `IProfileService`
* `SellerController.cs` -> injects `ISellerService`
* `AdminUsersController.cs` -> injects `IAdminUserService`

## 5. Absolute Architectural Prohibitions
* 🛑 Prohibit Repository Pattern over EF Core (inject `IUserDbContext` directly).
* 🛑 Prohibit MediatR & CQRS handlers.
* 🛑 Prohibit gRPC and Message Brokers (RabbitMQ/MassTransit).
* 🛑 Prohibit direct queries to other microservice databases.
