import type { BootOptions, UpdateOptions } from 'svelte-intercom';

export function buildIntercomDataUpdate(previousBootOptions: BootOptions, bootOptions: BootOptions): UpdateOptions {
    const update: Record<string, unknown> = {};
    addIntercomIdentity(update, bootOptions);

    for (const [key, value] of Object.entries(bootOptions)) {
        if (key === 'email' || key === 'userId') {
            continue;
        }

        if (!areIntercomValuesEqual(previousBootOptions[key], value)) {
            update[key] = value;
        }
    }

    return update as UpdateOptions;
}

export function buildIntercomRouteUpdate(bootOptions: BootOptions, now = Date.now()): UpdateOptions {
    const update: Record<string, unknown> = { lastRequestAt: Math.floor(now / 1000) };
    addIntercomIdentity(update, bootOptions);

    // Intercom's SPA guidance requires last_request_at for URL-only updates, but
    // svelte-intercom currently marks this supported field as `never` in its types.
    return update as UpdateOptions;
}

export function getIntercomRouteKey(routeId: null | string | undefined, pathname: string) {
    return routeId ?? pathname;
}

function addIntercomIdentity(update: Record<string, unknown>, bootOptions: BootOptions) {
    if (bootOptions.email) {
        update.email = bootOptions.email;
    }

    if (bootOptions.userId) {
        update.userId = bootOptions.userId;
    }
}

function areIntercomValuesEqual(previousValue: unknown, value: unknown) {
    if (Object.is(previousValue, value)) {
        return true;
    }

    if (typeof previousValue !== 'object' || previousValue === null || typeof value !== 'object' || value === null) {
        return false;
    }

    return JSON.stringify(previousValue) === JSON.stringify(value);
}
