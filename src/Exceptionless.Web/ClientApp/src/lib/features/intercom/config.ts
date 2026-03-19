export const intercomJwtLifetimeMs = 60 * 60 * 1000;
export const intercomTokenRefreshLeadTimeMs = 5 * 60 * 1000;
export const intercomTokenRefreshIntervalMs = intercomJwtLifetimeMs - intercomTokenRefreshLeadTimeMs;
