# Backend Guidelines (C#)

## Patterns & Practices

- Respect existing layering: keep domain logic in `Exceptionless.Core`, concrete implementations in `Exceptionless.Insulation`, hosting concerns in `Exceptionless.Web`, and background work in `Exceptionless.Job`.
- Reuse existing services and option classes; prefer dependency injection over static access.
- When touching configuration, prefer appsettings and options binding; avoid hardcoding secrets or connection info.

## Dependencies

- NuGet feeds come from `NuGet.Config`; do not add sources unless requested. Keep versions aligned with `src/Directory.Build.props`.
- Avoid introducing new packages unless necessary; prefer existing dependencies already in the solution.
