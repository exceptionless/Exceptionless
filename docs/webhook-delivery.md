Webhook delivery uses the configured URL unchanged, including its path and query string. Existing private receivers, redirects, and proxy settings remain supported. Cookies returned by one receiver are not stored or sent with later deliveries.

The `Destination` log field and HTTP trace URL contain the origin (scheme, host, and port). Webhook paths and queries can contain credentials; for example, Slack treats its incoming webhook URL as a secret. Use the webhook identifier to find the complete configured URL when troubleshooting.

Delivery logs retain organization/project context, status, and original exceptions. HTTP traces retain method, duration, status, and error information. Only duplicate framework HTTP client logs are disabled because their URL paths are not redacted by default. Exception details are preserved, so this is not a guarantee that every diagnostic string is redacted.

Only response headers are needed to determine delivery status. Response bodies are not buffered, and responses are disposed even when failure bookkeeping throws.
