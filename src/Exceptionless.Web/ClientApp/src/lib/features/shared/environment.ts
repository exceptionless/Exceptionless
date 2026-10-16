import { browser } from '$app/env';
import {
    PUBLIC_BASE_URL as configuredBaseUrl,
    PUBLIC_EXCEPTIONLESS_API_KEY as configuredExceptionlessApiKey,
    PUBLIC_EXCEPTIONLESS_SERVER_URL as configuredExceptionlessServerUrl
} from '$app/env/public';

type OverridableVariable = 'PUBLIC_BASE_URL' | 'PUBLIC_EXCEPTIONLESS_API_KEY' | 'PUBLIC_EXCEPTIONLESS_SERVER_URL';

/** A value saved in localStorage lets a browser target another environment without a rebuild. */
export function getEnvironmentOverride(name: OverridableVariable): string | undefined {
    return (browser && localStorage.getItem(name)) || undefined;
}

export const PUBLIC_BASE_URL = getEnvironmentOverride('PUBLIC_BASE_URL') ?? configuredBaseUrl;
export const PUBLIC_EXCEPTIONLESS_API_KEY = getEnvironmentOverride('PUBLIC_EXCEPTIONLESS_API_KEY') ?? configuredExceptionlessApiKey;
export const PUBLIC_EXCEPTIONLESS_SERVER_URL = getEnvironmentOverride('PUBLIC_EXCEPTIONLESS_SERVER_URL') ?? configuredExceptionlessServerUrl;
