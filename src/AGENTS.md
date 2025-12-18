# Backend Guidelines (C#)

## Architecture & Layering

```text
Exceptionless.Core        → Domain logic, services, interfaces
Exceptionless.Insulation  → Concrete implementations (ES, Redis, etc.)
Exceptionless.Web         → ASP.NET Core host, controllers, middleware
Exceptionless.Job         → Background Jobs
```

- Keep domain logic in `Core`; concrete implementations in `Insulation`
- Prefer dependency injection over static access
- Use appsettings and options binding; never hardcode secrets

## Dependencies

- NuGet feeds from `NuGet.Config`; do not add sources unless requested
- Keep versions aligned with `src/Directory.Build.props`
- Avoid new packages unless necessary; prefer existing dependencies
