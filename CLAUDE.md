# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Structure

Monorepo with two independent apps:

```
backend/    .NET 10 Web API (DevBlog.Api) — Minimal APIs, EF Core, SQLite
frontend/   Angular 22 app (devblog-ui) — standalone components
```

## Commands

### Backend (from repo root)

```bash
dotnet run --project backend/src/DevBlog.Api/DevBlog.Api.csproj
dotnet build backend/DevBlog.slnx
```

Migrations are applied automatically on startup (`db.Database.Migrate()` in `Program.cs`), and `DataSeeder.Seed(db)` seeds an admin user + sample posts/comments if the `Users` table is empty. To add a new EF Core migration:

```bash
dotnet ef migrations add <Name> --project backend/src/DevBlog.Api/DevBlog.Api.csproj
```

There is no test project in this repo currently.

### Frontend

```bash
cd frontend/devblog-ui
npm install
npm start          # ng serve
npm run build       # ng build
npm run watch        # ng build --watch --configuration development
```

## Architecture

### Backend (`backend/src/DevBlog.Api`)

- Minimal API style: no controllers. Each resource has a static `*Endpoint` class with a `Map(WebApplication app)` method that registers its routes, called from `Program.cs` (`PostsEndpoint`, `CommentsEndpoint`, `AuthEndpoint`).
- `Data/AppDbContext.cs` — single EF Core `DbContext` with `Users`, `Posts`, `Comments` `DbSet`s. Relationships (`Post.Author`, `Comment.Post`) are configured in `OnModelCreating`.
- `Models/` — plain EF entities (`User`, `Post`, `Comment`).
- Auth is JWT bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`). `AuthEndpoint` issues tokens on `/auth/login`; claims are `NameIdentifier` (user id), `Name`, `Role`. Endpoints that require auth call `.RequireAuthorization()` (e.g. `POST /posts`).
- **Known dev-only shortcuts, not yet hardened**: the JWT signing secret is hardcoded in both `Program.cs` and `AuthEndpoint.cs` (must stay in sync if changed — better to extract to config); passwords are stored as base64 (not hashed); CORS allows any origin/method/header. Treat these as things to fix if asked to touch auth/security, not as intended design.
- SQLite DB file (`devblog.db`) lives alongside the project and is created/migrated automatically at startup.

### Backend architecture decision: Endpoint → Service → Repository

Target layering for the backend (applies to new/modified endpoints going forward):

- **Endpoints must not inject `AppDbContext` directly.** They inject a service interface and call into it.
- **Services** hold business logic and depend on repositories, not on `DbContext`.
- **Repositories** are the only layer that touches `DbContext`/EF Core directly. Use a **generic repository** (e.g. `IRepository<T>`) as the default implementation approach rather than one bespoke repository per entity, unless an entity's query needs genuinely don't fit the generic shape.

This is the intended direction, not the current state — see technical debt below.

#### Technical debt: current code does not follow this pattern

Every endpoint class today injects `AppDbContext` directly and has no service or repository layer at all:

- `Endpoints/PostsEndpoint.cs` — all three routes (`GET /posts`, `GET /posts/{slug}`, `POST /posts`) query/write via `AppDbContext` directly.
- `Endpoints/CommentsEndpoint.cs` — `POST /posts/{slug}/comments` queries/writes via `AppDbContext` directly.
- `Endpoints/AuthEndpoint.cs` — `POST /auth/login` queries `AppDbContext` directly.

When asked to add or meaningfully touch an endpoint, prefer introducing the service/repository layering for that slice rather than extending the direct-`DbContext` pattern further. Don't do a big-bang rewrite of unrelated endpoints unless asked.

#### Technical debt: no test project

There is currently no test project for the backend. Going forward, backend tests should use **xUnit**, with a target of **70% code coverage**. When asked to add tests or meaningfully touch backend code, prefer setting up/extending an xUnit test project over leaving the gap. Don't block unrelated work on retroactively reaching the coverage target.

### Frontend (`frontend/devblog-ui/src/app`)

- Standalone Angular components (no NgModules), lazy-loaded per route in `app.routes.ts`.
- Pages live under `pages/<name>/` with a co-located `.component.ts` + `.component.html` (post-list, post-detail, login).
- `services/auth.service.ts` holds the JWT in `localStorage` and exposes `authInterceptor`, an `HttpInterceptorFn` registered in `app.config.ts` via `provideHttpClient(withInterceptors([authInterceptor]))` — it attaches `Authorization: Bearer <token>` to every outgoing request when a token is present.
- `services/post.service.ts` calls the backend's `/posts` endpoints.
- API base URL comes from `environments/environment.ts` (prod) / `environment.development.ts` (dev) as `apiUrl`.

### Cross-cutting

- The frontend's `environment.apiUrl` must match wherever the backend is actually listening; there's no proxy config in this repo, so mismatches are a common source of CORS/connection issues during local dev.
- `POST /posts` requires the `Content-Type: application/json` body to match `CreatePostRequest(Title, Content, Slug, Tags)`; there is no slug-uniqueness check server-side (noted as a TODO in `PostsEndpoint.cs`).

## Code Quality

Apply these when writing or modifying code in this repo, backend and frontend alike:

- **DRY** — don't duplicate logic across endpoints/services/components; extract a shared method or service once the same logic appears a second time, not preemptively.
- **Naming conventions** — follow each language's own convention rather than importing the other's: PascalCase for C# types/methods/properties, camelCase for C# locals/parameters; camelCase for TypeScript variables/methods, PascalCase for TypeScript classes/interfaces/components. Names should say what the thing is/does without needing a comment.
- **KISS** — prefer the straightforward solution over a clever or heavily abstracted one; don't introduce patterns (generic repository included) ahead of an actual second use case.
