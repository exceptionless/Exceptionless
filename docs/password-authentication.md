# Password authentication

Interactive login and Basic password authentication share admission state. Each account allows five failed checks per quarter-hour window; each client IP allows fifteen. Checks underway reserve capacity before validating credentials. Successful checks return their capacity and clear account failures observed at admission; IP failures remain. Account recovery clears completed account failures.

Basic password authentication retries when checks underway occupy capacity, for up to two seconds with at most 100 waiting requests per authentication service instance. When completed failures exhaust the budget, admission is denied without waiting. Interactive login retains immediate admission or denial; it does not use the Basic authentication waiting queue. In-flight cache operations remain subject to the provider's own timeout.

Configure Redis for deployments with multiple API hosts so admission state is shared. In-memory caching coordinates only callers within the same process. Cancellation releases partial reservations. Pending reservations from a stopped host remain charged until the window ends.

Clients behind a shared IP reuse capacity after successful checks. Fifteen actual failures from that IP still affect its other clients until the next window. The bounded wait addresses concurrent successful Basic authentication bursts; it does not change that failure policy or guarantee admission under sustained load.
