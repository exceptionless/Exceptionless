Webhooks deliver to user-supplied HTTP or HTTPS URLs, including private receivers. No destination allowlist or private-network override is required. Redirects and proxy settings use the standard HTTP client behavior. Cookies are not shared between deliveries.

Delivery logs identify the destination origin instead of recording URL credentials, paths, or queries. Automatic HTTP delivery traces and client logs are omitted. Failures retain the original exception and message for troubleshooting; exception details are not redacted.

Only response headers are needed to determine delivery status. Response bodies are not buffered, and responses are disposed even when failure bookkeeping throws.
