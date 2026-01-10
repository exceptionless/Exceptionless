---
name: Security Principles
description: |
  Security best practices for the Exceptionless codebase. Secrets management, input validation,
  secure defaults, and avoiding common vulnerabilities.
  Keywords: security, secrets, encryption, PII, logging, input validation, secure defaults,
  environment variables, OWASP, cryptography
---

# Security Principles

## Secrets Management

Secrets are injected via Kubernetes ConfigMaps and environment variables — never commit secrets to the repository.

- **Configuration files** — Use `appsettings.yml` for non-secret config
- **Environment variables** — Secrets injected at runtime via `EX_*` prefix
- **Kubernetes** — ConfigMaps mount configuration, Secrets mount credentials

```csharp
// AppOptions binds to configuration (including env vars)
public class AppOptions
{
    public string? StripeApiKey { get; set; }
    public AuthOptions Auth { get; set; } = new();
}
```

## Validate All Inputs

- Check bounds and formats before processing
- Use `ArgumentNullException.ThrowIfNull()` and similar guards
- Validate early, fail fast

## Sanitize External Data

- Never trust data from queues, caches, user input, or external sources
- Validate against expected schema
- Sanitize HTML/script content before storage or display

## No Sensitive Data in Logs

- Never log passwords, tokens, API keys, or PII
- Log identifiers and prefixes, not full values
- Use structured logging with safe placeholders

## Use Secure Defaults

- Default to encrypted connections (SSL/TLS enabled)
- Default to restrictive permissions
- Require explicit opt-out for security features

## Avoid Deprecated Cryptographic Algorithms

Use modern cryptographic algorithms:

- ❌ `MD5`, `SHA1` — Cryptographically broken
- ✅ `SHA256`, `SHA512` — Current standards

## Avoid Insecure Serialization

- ❌ `BinaryFormatter` — Insecure deserialization vulnerability
- ✅ `System.Text.Json`, `Newtonsoft.Json` — Safe serialization

## Input Bounds Checking

- Enforce minimum/maximum values on pagination parameters
- Limit batch sizes to prevent resource exhaustion
- Validate string lengths before storage

## OWASP Reference

Review [OWASP Top 10](https://owasp.org/www-project-top-ten/) regularly:

1. Broken Access Control
2. Cryptographic Failures
3. Injection
4. Insecure Design
5. Security Misconfiguration
6. Vulnerable and Outdated Components
7. Identification and Authentication Failures
8. Software and Data Integrity Failures
9. Security Logging and Monitoring Failures
10. Server-Side Request Forgery
