# HousingHub – Agent Guide

## Architecture Overview

Clean Architecture with 7 projects:

| Project | Role |
|---|---|
| `HousingHub.API` | Controllers, Hubs, middleware, entry point |
| `HousingHub.Application` | CQRS commands/queries, MediatR handlers, FluentValidation |
| `HousingHub.Service` | Business logic services, DTOs, AutoMapper profiles |
| `HousingHub.Repository` | Repository implementations, UnitOfWork |
| `HousingHub.Data` | DynamoDB context, table initialization, repo interfaces |
| `HousingHub.Model` | Domain entities, enums |
| `HousingHub.Core` | Shared response wrappers, custom exceptions |

Request flow: **Controller → MediatR → Handler → Service → Repository → DynamoDB**

## Database: AWS DynamoDB (not SQL)

- Entities use `[DynamoDBTable]`, `[DynamoDBHashKey]`, `[DynamoDBGlobalSecondaryIndexHashKey]`, `[DynamoDBVersion]` attributes
- All entities inherit `BaseEntity` (Id, DateCreated, DateModified, IsActive, VersionNumber)
- Tables auto-created on startup via `DynamoDbTableInitializer`
- `UnitOfWork.SaveAsync()` is a **no-op** — DynamoDB persists immediately per repository call; do not rely on transaction semantics
- Entity timestamps are managed in `GenericCommandRepository`, not manually

## CQRS Pattern

Every feature in `HousingHub.Application` has:
1. Command/Query record implementing `IRequest<BaseResponse<T>>`
2. Handler implementing `IRequestHandler<TRequest, TResponse>`
3. FluentValidation validator class

Example: `src/HousingHub.Application/Property/Commands/CreateProperty/`

Pipeline behaviors (registered via `ConfigureServices.cs`): `ValidationBehaviour` → `LoggingBehaviour` → `PerformanceBehaviour`

## Response Conventions

All endpoints return `BaseResponse<T>` or `BaseResponsePagination<T>` from `HousingHub.Core`:

```csharp
// Success
return new BaseResponse<T> { IsSuccessful = true, Data = result, Message = "..." };
// Use static constants from ResponseMessages for Message values
```

Use `ResponseMessages` static class for all message strings — never hardcode message text.

Global exception handling via `ExceptionHandlingMiddleware` (ValidationException → 400, others → 500).

## Controllers

- Route: `api/v{version:apiVersion}/[Controller]`, versioned (`[ApiVersion("1.0")]`)
- Inject `IMediator`, dispatch commands/queries — no direct service calls
- Extract userId from JWT: `User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value`
- Authorization: `[Authorize(Policy = "PropertyOwnerOrAgent")]` for owner/agent actions; default requires JWT bearer
- File uploads: `[FromForm]` + `[Consumes("multipart/form-data")]`

## Real-Time (SignalR)

Two hubs at `/hubs/notifications` and `/hubs/chat`. JWT passed via query string (`?access_token=`) for WebSocket auth — handled by custom event in `Program.cs`. Use `IRealtimeNotifier` and `IChatRealtimeNotifier` service abstractions; never call hub contexts directly from services.

## Authentication

- Primary: JWT Bearer (secret/issuer/audience from config, zero clock skew)
- Secondary: Google OAuth (cookie scheme `"ExternalAuth"`, temp session)
- Policy `"PropertyOwnerOrAgent"` checks claim `customer_type` for `"HouseOwner"` or `"Agent"`

## Registering New Features

Each project has a `ConfigureServices.cs` extension method called in `Program.cs`. Add new services there:

- Business services → `HousingHub.Service/ConfigureServices.cs` (Scoped)
- Repositories → `HousingHub.Repository/ConfigureServices.cs` (Scoped)
- MediatR handlers/validators are auto-discovered by assembly scan — no manual registration needed

## Testing

Tests in `src/HousingHub.Test/`, organized by domain (Property/, Customer/, Inspection/, etc.). Use Moq for `IUnitOfWork` and external services; use real AutoMapper profiles. Do not mock the repository at the DynamoDB level — mock at the `IUnitOfWork` abstraction.

## Build & Run

```bash
dotnet build HousingHub.sln
dotnet test src/HousingHub.Test/HousingHub.Test.csproj
dotnet run --project src/HousingHub.API
```

AWS credentials and config required in `appsettings.json` or environment variables for DynamoDB, S3 (file storage), SendGrid (email).
