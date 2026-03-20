export const intercomJwtLifetimeMs = 60 * 60 * 1000;
export const intercomTokenRefreshLeadTimeMs = 5 * 60 * 1000;
export const intercomTokenRefreshIntervalMs = intercomJwtLifetimeMs - intercomTokenRefreshLeadTimeMs;

export function getIntercomTokenSessionKey(accessToken: null | string) {
    if (!accessToken) {
        return null;
    }

    let hash = 0;
    for (const character of accessToken) {
        hash = (hash * 31 + character.charCodeAt(0)) >>> 0;
    }

    return hash.toString(36);
}

export function shouldLoadIntercomOrganization(intercomAppId: null | string | undefined, isIntercomTokenReady: boolean) {
    return !!intercomAppId && isIntercomTokenReady;
}
