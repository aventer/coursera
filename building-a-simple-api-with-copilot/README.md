# User Management API

A small ASP.NET Core Web API for registering users, logging in with a JWT, and managing your own account. It was built as a Coursera course project ("Building a Simple API with Copilot").

> **Course project:** this repository is not accepting contributions. See [Contributors.md](Contributors.md).

## Features

- **Registration and login**: anyone can register; logging in exchanges an email and password for a signed JWT bearer token.
- **Protected endpoints**: every other endpoint requires a valid token.
- **Self-service only**: users can update or delete their own account, not anyone else's.
- **Secure password storage**: passwords are hashed with BCrypt (work factor 12) and never returned by the API.
- **URL-friendly Ids**: a user's Id is a SHA-256 hash of a random seed plus their email, encoded as unpadded base64url (43 characters).
- **Request logging**: every HTTP call is logged to the console with its method, path, status code and duration.
- **Swappable storage**: data lives in an in-memory store behind an `IUserRepository` interface that dependency injection provides.

## Tech stack

- .NET 10 / ASP.NET Core (controllers)
- `Microsoft.AspNetCore.Authentication.JwtBearer` for token validation
- `BouncyCastle.Cryptography` for BCrypt password hashing
- `Microsoft.AspNetCore.OpenApi` for the OpenAPI document

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run

```bash
dotnet run --launch-profile http
```

The API listens on `http://localhost:5094`; the `https` profile also listens on `https://localhost:7164`. In Development, the OpenAPI document is served at `/openapi/v1.json`.

### Configuration

JWT settings come from the `Jwt` section of configuration:

| Setting              | Description                                       | Default             |
| -------------------- | ------------------------------------------------- | ------------------- |
| `Jwt:Issuer`         | Token issuer                                      | `UserManagementApi` |
| `Jwt:Audience`       | Token audience                                    | `UserManagementApi` |
| `Jwt:Key`            | Symmetric signing key, **at least 32 characters** | Development only    |
| `Jwt:ExpiresMinutes` | Token lifetime in minutes                         | `60`                |

The app refuses to start if `Jwt:Key` is missing or too short. The key in `appsettings.Development.json` is for local development only. Anywhere else, supply your own, for example with user secrets or an environment variable:

```bash
dotnet user-secrets set "Jwt:Key" "<a random string of at least 32 characters>"
# or
export Jwt__Key="<a random string of at least 32 characters>"
```

## API

All routes are under `/api/users`. Routes marked 🔒 need an `Authorization: Bearer <token>` header.

| Method   | Route                   | Auth | Description                     | Responses                         |
| -------- | ----------------------- | ---- | ------------------------------- | --------------------------------- |
| `POST`   | `/api/users`            |      | Register a user                 | `201`, `400`, `409` email taken   |
| `POST`   | `/api/users/login`      |      | Log in and receive a JWT        | `200`, `400`, `401`               |
| `GET`    | `/api/users`            | 🔒   | List all users                  | `200`, `401`                      |
| `GET`    | `/api/users/{id}`       | 🔒   | Get one user                    | `200`, `401`, `404`               |
| `PUT`    | `/api/users/{id}`       | 🔒   | Update yourself (partial)       | `200`, `400`, `401`, `403`, `404`, `409` |
| `DELETE` | `/api/users/{id}`       | 🔒   | Delete yourself                 | `204`, `401`, `403`, `404`        |

### Request and response bodies

```jsonc
// POST /api/users
{ "name": "Jane Doe", "email": "jane@example.com", "password": "Sup3rSecret!" }

// POST /api/users/login
{ "email": "jane@example.com", "password": "Sup3rSecret!" }
// -> { "token": "eyJ...", "expiresAt": "2026-10-27T12:00:00Z" }

// PUT /api/users/{id}: send only the fields you want to change
{ "name": "Jane Smith" }

// User responses never include the password
{ "id": "W0HujyqO-R-j8jRO8awzvoikhRE8v0FH9eyl9tLiO0s", "name": "Jane Doe", "email": "jane@example.com" }
```

Validation rules:

- **Name**: 1–100 characters.
- **Email**: a valid address, up to 254 characters. Emails are trimmed and lowercased, so `JANE@example.com` and `jane@example.com` count as the same user.
- **Password**: 8–72 characters. BCrypt only uses the first 72 bytes.

### Changing your email changes your Id

The Id is derived from the email, so a successful email change returns a **new Id** and the old one stops working. Your current token still carries the old Id, so log in again before making further changes to your account.

## Testing

[`UserManagementApi.http`](UserManagementApi.http) is an end-to-end test suite of 34 requests. It covers registration, login, authentication, authorization, updates, email and password changes, and deletion, including the error cases. Each request's title states the status code it should return, for example `-> 409 Conflict`.

1. Start the API with `dotnet run --launch-profile http`.
2. Open `UserManagementApi.http` in Visual Studio, Rider, or VS Code with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension.
3. Send the requests **from top to bottom**. Later requests reuse tokens and Ids captured from earlier ones.

The store is in-memory, so restart the API before running the suite again. Otherwise the registration requests return `409`.

## Project structure

```text
Controllers/
  UsersController.cs     HTTP endpoints, authorization checks
Models/
  User.cs                User entity: Id derivation, password hashing and verification
  UserDtos.cs            Request and response records with validation attributes
Services/
  UserService.cs         Business rules (unique emails, updates, re-keying)
  UserRepository.cs      IUserRepository and the in-memory implementation
  TokenService.cs        JWT options and token creation
Program.cs               Dependency injection, authentication, logging and the middleware pipeline
UserManagementApi.http   End-to-end request suite
```

## Swapping the data store

Storage is behind `IUserRepository`, which is registered in `Program.cs`:

```csharp
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
```

To use a real database, implement `IUserRepository` (for example `EfUserRepository` over an EF Core `DbContext`) and change that registration. The service and controller don't change. `UserService` is already scoped, so it works with scoped repositories. Enforce email uniqueness with a unique index; `TryAdd` should return `false` when the index rejects an insert.

## License

MIT. See the [LICENSE](../LICENSE) file at the repository root.
