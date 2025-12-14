# Web API Guidelines (Exceptionless.Web)

## Scope

Applies to the ASP.NET Core host in `src/Exceptionless.Web`, including controllers, middleware, configuration, and API-facing code. The Svelte SPA lives in `ClientApp`; the legacy Angular client is under `ClientApp.angular` (avoid changes there unless explicitly requested).

## API Development

- **Controllers:** Keep thin; validate inputs, delegate to services in `Exceptionless.Core`/`Exceptionless.Insulation`
- **DTOs:** Reuse existing types and validation attributes; avoid duplicating types shared with SPA generators
- **Routes:** Respect existing routes and versioning; coordinate breaking changes

### Error Handling

- Return `ProblemDetails` for API errors (RFC 7807 format)
- Use FluentValidation or data annotations for input validation
- Let validation middleware handle `400 Bad Request` responses automatically
- Log errors with correlation IDs for traceability

### Security

- Never expose secrets in responses or logs
- Validate and sanitize all inputs at the API boundary
- Apply authorization consistently on all endpoints
- Use structured logging; avoid verbose logs on hot paths

## SPA Hosting

- Svelte SPA is the primary client—coordinate API changes with `ClientApp`
- Regenerate API types when contracts change: `npm run generate-models`
- Leave `ClientApp.angular` untouched unless explicitly maintaining legacy paths
