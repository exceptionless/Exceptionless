# Exceptionless query parameter state

Originally derived from [beynar/kit-query-params](https://github.com/beynar/kit-query-params) version 0.0.26 at commit `7c90edf7`. The original project is MIT licensed; see [LICENSE](./LICENSE).

This is now Exceptionless-owned code intentionally limited to the behavior used by the application:

- top-level string, number, boolean, date, and enum parameters;
- typed property access and atomic multi-parameter updates;
- unknown URL parameter preservation;
- debounced push or replace navigation;
- two-way synchronization with browser navigation;
- no state or URL writes for unchanged values after coercion;
- cancellation of pending synchronization during navigation and component teardown.
