# Password authentication

Interactive login and Basic password authentication share admission state. Each account allows five failed checks per quarter-hour window; each client IP allows fifteen. Checks underway reserve capacity before validating credentials. Successful checks return their capacity, and account recovery clears completed account failures.

When capacity is occupied by checks underway, requests wait for up to ten seconds and retry before returning the existing login-denied response. Completed failures still deny admission. Cancellation releases any partial reservation. Pending reservations from a stopped host remain charged until the window ends.

Configure Redis for deployments with multiple API hosts so admission state is shared. In-memory caching coordinates only callers within the same process. The installed Foundatio 13.0.4 in-memory increment primitive loses updates under contention; admission currently uses conditional cache operations instead.

Clients behind a shared IP reuse capacity after successful checks. Fifteen actual failures from that IP still affect its other clients until the next window. The bounded wait addresses concurrent successful bursts; it does not change that failure policy or guarantee admission under sustained load.
