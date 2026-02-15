# BonyadRazavi Portal (.NET 10)

A production-style starter solution based on .NET 10, Blazor Web App, layered Auth service, and YARP Gateway.

## Solution Layout

- `src/BuildingBlocks/BonyadRazavi.Shared`
  - Shared contracts (login request/response DTOs)
- `src/Services/Auth/BonyadRazavi.Auth.Domain`
  - Domain entities
- `src/Services/Auth/BonyadRazavi.Auth.Application`
  - Use-cases, interfaces, authentication business logic
- `src/Services/Auth/BonyadRazavi.Auth.Infrastructure`
  - In-memory user repository and password hashing implementation
- `src/Services/Auth/BonyadRazavi.Auth.Api`
  - Auth microservice (JWT issuing + `/api/auth` endpoints)
- `src/Web/BonyadRazavi.WebApp`
  - Blazor Web Application with RTL Persian login UI and dashboard
- `src/Gateway/BonyadRazavi.Gateway`
  - YARP Reverse Proxy entry point
- `tests/BonyadRazavi.Auth.Application.Tests`
  - Unit tests for authentication use-case

## Gateway Routing (YARP)

Configured in `src/Gateway/BonyadRazavi.Gateway/appsettings.json`:

- `/api/auth/{**catch-all}` -> `https://localhost:7115/` (Auth API)
- `/{**catch-all}` -> `https://localhost:7197/` (Blazor WebApp)

This means users only need Gateway URL in browser.

## Run in Visual Studio 2026

1. Open `New_Src/BonyadRazavi.Portal10/BonyadRazavi.Portal.slnx`.
2. Set startup projects to **Multiple startup projects**:
   - `BonyadRazavi.Auth.Api` (Start)
   - `BonyadRazavi.WebApp` (Start)
   - `BonyadRazavi.Gateway` (Start)
3. Run the solution.
4. Open Gateway URL:
   - `https://localhost:7100/login`

## Demo Credentials

- Username: `admin`
- Password: `Razavi@1404`

## Useful Endpoints

- Gateway health: `https://localhost:7100/gateway/health`
- Auth health: `https://localhost:7115/health`
- Login API (through gateway): `https://localhost:7100/api/auth/login`

## CLI Commands

```bash
dotnet build New_Src/BonyadRazavi.Portal10/BonyadRazavi.Portal.slnx
dotnet test New_Src/BonyadRazavi.Portal10/BonyadRazavi.Portal.slnx
```