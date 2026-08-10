import { building } from '$app/environment';
import { invalidateAll as _invalidateAll, afterNavigate, beforeNavigate, goto, invalidate, pushState, replaceState } from '$app/navigation';
import { page } from '$app/stores';
import { SvelteURLSearchParams } from 'svelte/reactivity';
import { get } from 'svelte/store';

import type { Default, Opts, Schema, SchemaOutput } from './types.js';

import { coerceObject } from './coerce.js';
import { createProxy } from './proxy.js';
import { clearSearchParamPaths, debounce, isValidPath, parseURL, setSearchParamIfChanged } from './utils.js';

export const queryParamsState = <T extends Schema, D extends Default<T> | undefined, Enforce extends boolean = false>({
    debounce: debounceTime = 200,
    default: _defaultValue,
    enforceDefault,
    invalidate: invalidations = [],
    invalidateAll = false,
    preserveUnknownParams = true,
    pushHistory = false,
    schema,
    shallow = false,
    twoWayBinding = true
}: Opts<T, D, Enforce>) => {
    const defaultValue = _defaultValue ? coerceObject(schema, _defaultValue) : undefined;
    const url = building ? new URL('https://github.com/beynar/kit-state-params') : get(page).url;
    const current = $state<SchemaOutput<T, D, Enforce>>(parseURL<T, D, Enforce>(url, schema, defaultValue as D));
    const searchParams = new SvelteURLSearchParams(url.search);

    const cleanUnknownParams = () => {
        if (preserveUnknownParams) {
            return;
        }

        Array.from(searchParams.keys()).forEach((key) => {
            if (!isValidPath(key, schema)) {
                searchParams.delete(key);
            }
        });
    };

    cleanUnknownParams();

    // Sync the search params and the state with changes that occurs outside of a state mutation
    twoWayBinding &&
        afterNavigate(async ({ complete, to }) => {
            if (!to) {
                return;
            }

            await complete;

            const newSearchParams = new URLSearchParams(to.url.search);
            if (newSearchParams.toString() !== searchParams.toString()) {
                let hasChanged = false;
                Array.from(newSearchParams.keys()).forEach((key) => {
                    const isValid = isValidPath(key, schema);
                    if (!isValid && !preserveUnknownParams) {
                        // Remove unknown params
                        newSearchParams.delete(key);
                    } else if (searchParams.get(key) !== newSearchParams.get(key)) {
                        // Assign changed params if they are not already in the search params
                        searchParams.set(key, newSearchParams.get(key)!);
                        if (isValid) {
                            hasChanged = true;
                        }
                    }
                });
                // Clean up remaining search params
                Array.from(searchParams.keys()).forEach((key) => {
                    if (!newSearchParams.has(key)) {
                        searchParams.delete(key);
                        if (isValidPath(key, schema)) {
                            hasChanged = true;
                        }
                    }
                });
                // Update the state if the search params have changed
                hasChanged && Object.assign(current, parseURL(newSearchParams, schema));
            }
        });

    const sync = () => {
        cleanUnknownParams();
        const query = searchParams.toString();
        const hash = window.location.hash;
        const currentSearchParams = new URLSearchParams(window.location.search);
        if (query !== currentSearchParams.toString()) {
            if (shallow) {
                (pushHistory ? pushState : replaceState)(`?${query}${hash}`, {});
                if (invalidateAll) {
                    _invalidateAll();
                }
            } else {
                goto(`?${query}${hash}`, {
                    invalidateAll,
                    keepFocus: true,
                    noScroll: true,
                    replaceState: !pushHistory
                });
            }

            invalidations.forEach(invalidate);
        }
    };

    const debouncedSync = debounce(sync, debounceTime);
    beforeNavigate(() => {
        debouncedSync.cancel();
    });

    const reset = (_enforceDefault = enforceDefault) => {
        const previousSearch = searchParams.toString();
        Array.from(searchParams.keys()).forEach((key) => {
            const isValid = isValidPath(key, schema);
            if (isValid || (!isValid && !preserveUnknownParams)) {
                searchParams.delete(key);
            }
        });
        Object.assign(current, parseURL(searchParams, schema, _enforceDefault ? defaultValue : undefined));
        if (searchParams.toString() !== previousSearch) {
            debouncedSync();
        }
    };

    const updateSearchParams = (key: string, stringified: null | string) => {
        if (setSearchParamIfChanged(searchParams, key, stringified)) {
            debouncedSync();
        }
    };

    return createProxy<T, D, Enforce>(current, {
        clearPaths: (path) => {
            if (clearSearchParamPaths(searchParams, path)) {
                debouncedSync();
            }
        },
        default: defaultValue,
        enforceDefault,
        onUpdate: updateSearchParams,
        reset,
        schema,
        searchParams,
        sync
    });
};
