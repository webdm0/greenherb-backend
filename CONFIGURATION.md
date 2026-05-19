# Configuration

## Secret policy
- Do not store secrets in `appsettings*.json`.
- Local development: use `.env` at the repository root.
- Production: use platform environment variables or a secret manager.
- Keep `appsettings.json` only for non-secret defaults and structure.
- `.env.example` is a local-friendly template.

## Required secret variables
- `Jwt__Key`
- `SessionHint__Key`
- `Stripe__SecretKey`
- `Stripe__WebhookSecret`
- `FrontendSettings__RevalidateSecret`

## Required non-secret variables
- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Authentication__Google__ClientIds__0`
- `AllowedOrigins__0`
- `FrontendSettings__RevalidateUrl`

## PostgreSQL variables for local Docker Compose
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `POSTGRES_PORT`

## Optional application variables
- `Jwt__AccessTokenMinutes`
- `Jwt__RefreshTokenDays`
- `Jwt__MaxSessions`
- `SessionHint__TtlSeconds`
- `SessionHint__Issuer`
- `SessionHint__Audience`
- `Cookies__UseCrossSiteAuth`
- `Cookies__UseSecureAuthCookies`
- `CatalogSeed__OnStartup`

## Runtime environment variables
- `PORT` to bind the app to `http://0.0.0.0:<PORT>`
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` when the app runs behind a reverse proxy

## Local `.env` usage
- Copy `.env.example` to `.env`.
- Fill the required values in `.env`.
- Start PostgreSQL with `docker compose up -d`.
- Apply migrations and run the API with `dotnet run --project src/GreenHerb.Api`.
- `.env` is ignored by git.

## Configuration constraints
- `SessionHint__Key` must be different from `Jwt__Key`.
- `Cookies__UseCrossSiteAuth=true` requires `Cookies__UseSecureAuthCookies=true`.

## Container usage
```bash
docker build -t greenherb-api .
docker run --rm -p 10000:10000 --env-file .env greenherb-api
```
