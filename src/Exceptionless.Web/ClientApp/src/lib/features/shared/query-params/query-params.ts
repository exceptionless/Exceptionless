import type { QueryParameterInput, QueryParameterSchema, QueryParameterState, QueryParameterType, QueryParameterValue } from './types.js';

import { coerceQueryParameter, getListItemType } from './coerce.js';

export interface DebouncedFunction {
    (): void;
    cancel: () => void;
}

export interface QueryParameterUpdate<T extends QueryParameterSchema> {
    searchParams: URLSearchParams;
    state: QueryParameterState<T>;
    stateChanged: boolean;
    urlChanged: boolean;
}

export function applyQueryParameterUpdates<T extends QueryParameterSchema>(
    currentState: QueryParameterState<T>,
    currentSearchParams: URLSearchParams,
    values: Partial<QueryParameterInput<T>>,
    schema: T,
    defaults?: Partial<QueryParameterInput<T>>
): QueryParameterUpdate<T> {
    const searchParams = createSearchParams(currentSearchParams);
    const state = { ...currentState };
    const stateValues = state as Record<string, QueryParameterValue>;
    let stateChanged = false;

    for (const [key, value] of Object.entries(values)) {
        if (!Object.hasOwn(schema, key)) {
            continue;
        }

        const parameterType = schema[key];
        if (!parameterType) {
            continue;
        }

        const coerced = coerceQueryParameter(parameterType, value);
        if (queryParameterValuesEqual(stateValues[key], coerced)) {
            continue;
        }

        stateValues[key] = coerced;
        stateChanged = true;
        setSearchParameter(searchParams, key, stringifyQueryParameter(parameterType, coerced, hasDefaultItems(defaults, key)));
    }

    return {
        searchParams,
        state,
        stateChanged,
        urlChanged: !searchParamsEqual(searchParams, currentSearchParams)
    };
}

export function createDebouncedFunction(callback: () => void, delayMilliseconds: number): DebouncedFunction {
    let timeout: ReturnType<typeof setTimeout> | undefined;

    const cancel = () => {
        if (timeout !== undefined) {
            clearTimeout(timeout);
            timeout = undefined;
        }
    };

    const debounced = () => {
        cancel();

        if (delayMilliseconds <= 0) {
            callback();
            return;
        }

        timeout = setTimeout(() => {
            timeout = undefined;
            callback();
        }, delayMilliseconds);
    };

    debounced.cancel = cancel;
    return debounced;
}

export function createSearchParams(value?: string | URLSearchParams): URLSearchParams {
    return new URLSearchParams(value);
}

/**
 * Applies the parameters an instance owns onto the live URL. Other instances and
 * unrelated code may have written their own parameters since this instance last
 * read the URL, so only the schema's keys are taken from `ownSearchParams`.
 */
export function mergeQueryParameters(
    liveSearchParams: string | URLSearchParams,
    ownSearchParams: URLSearchParams,
    schema: QueryParameterSchema
): URLSearchParams {
    const searchParams = createSearchParams(liveSearchParams);
    for (const key of Object.keys(schema)) {
        setSearchParameter(searchParams, key, ownSearchParams.getAll(key));
    }

    return searchParams;
}

export function parseQueryParameters<T extends QueryParameterSchema>(
    searchParams: URLSearchParams,
    schema: T,
    defaults?: Partial<QueryParameterInput<T>>
): QueryParameterState<T> {
    const result = {} as QueryParameterState<T>;
    const resultValues = result as Record<string, QueryParameterValue>;
    const defaultValues = defaults as Partial<Record<string, QueryParameterValue | undefined>> | undefined;

    for (const key of Object.keys(schema)) {
        const parameterType = schema[key];
        if (!parameterType) {
            continue;
        }

        const fallback = coerceQueryParameter(parameterType, defaultValues?.[key]);
        const value = getListItemType(parameterType) ? (searchParams.has(key) ? searchParams.getAll(key) : undefined) : searchParams.get(key);
        resultValues[key] = coerceQueryParameter(parameterType, value, fallback);
    }

    return result;
}

export function queryParameterValuesEqual(current: unknown, next: unknown): boolean {
    if (Array.isArray(current) && Array.isArray(next)) {
        return current.length === next.length && current.every((item, index) => queryParameterValueEqual(item, next[index]));
    }

    return queryParameterValueEqual(current, next);
}

export function searchParamsEqual(left: URLSearchParams, right: string | URLSearchParams): boolean {
    return left.toString() === createSearchParams(right).toString();
}

function hasDefaultItems(defaults: Partial<QueryParameterInput<QueryParameterSchema>> | undefined, key: string): boolean {
    const value = (defaults as Partial<Record<string, unknown>> | undefined)?.[key];
    return Array.isArray(value) && value.length > 0;
}

function queryParameterValueEqual(current: unknown, next: unknown): boolean {
    if (current instanceof Date && next instanceof Date) {
        return current.getTime() === next.getTime();
    }

    return Object.is(current, next);
}

/** Replaces every value of `key`, keeping the parameter at its existing position so unchanged URLs compare equal. */
function setSearchParameter(searchParams: URLSearchParams, key: string, values: readonly string[]): void {
    const entries = [...searchParams];
    const position = entries.findIndex(([name]) => name === key);
    const nextEntries = entries.filter(([name]) => name !== key);
    nextEntries.splice(position < 0 ? nextEntries.length : position, 0, ...values.map((value): [string, string] => [key, value]));

    for (const name of new Set(entries.map(([name]) => name))) {
        searchParams.delete(name);
    }

    for (const [name, value] of nextEntries) {
        searchParams.append(name, value);
    }
}

function stringifyQueryParameter(parameterType: QueryParameterType, value: QueryParameterValue, hasDefaultItems: boolean): string[] {
    const itemType = getListItemType(parameterType);
    if (itemType) {
        const items = (value as readonly QueryParameterValue[]).flatMap((item) => stringifyQueryParameter(itemType, item, false));
        // An absent list parameter falls back to its default on reload, so an
        // explicitly emptied list with a non-empty default needs a marker.
        return items.length === 0 && hasDefaultItems ? [''] : items;
    }

    switch (parameterType) {
        case 'boolean': {
            return value === null ? [] : [value ? 'true' : 'false'];
        }

        case 'date': {
            return value instanceof Date && !Number.isNaN(value.getTime()) ? [value.toISOString()] : [];
        }

        case 'number': {
            return typeof value === 'number' && Number.isFinite(value) ? [String(value)] : [];
        }

        case 'string': {
            return typeof value === 'string' ? [value] : [];
        }

        default: {
            return typeof value === 'string' && value ? [value] : [];
        }
    }
}
