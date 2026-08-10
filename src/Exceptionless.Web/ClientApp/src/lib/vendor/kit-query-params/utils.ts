import type { Primitive, PrimitiveValue, QueryParamValues, Schema, SchemaOutput } from './types.js';

import { coercePrimitive } from './coerce.js';

export interface DebouncedFunction {
    (): void;
    cancel: () => void;
}

export interface QueryParamUpdate<T extends Schema> {
    searchParams: URLSearchParams;
    state: SchemaOutput<T>;
    stateChanged: boolean;
    urlChanged: boolean;
}

export function applyQueryParamUpdates<T extends Schema>(
    currentState: SchemaOutput<T>,
    currentSearchParams: URLSearchParams,
    values: Partial<QueryParamValues<T>>,
    schema: T
): QueryParamUpdate<T> {
    const searchParams = new URLSearchParams(currentSearchParams);
    const state = { ...currentState };
    const stateValues = state as Record<string, PrimitiveValue>;
    let stateChanged = false;

    for (const [key, value] of Object.entries(values)) {
        if (!Object.hasOwn(schema, key)) {
            continue;
        }

        const primitive = schema[key];
        if (!primitive) {
            continue;
        }

        const coerced = coercePrimitive(primitive, value);
        if (valuesEqual(stateValues[key], coerced)) {
            continue;
        }

        stateValues[key] = coerced;
        stateChanged = true;
        setSearchParam(searchParams, key, stringifyPrimitive(primitive, coerced));
    }

    return {
        searchParams,
        state,
        stateChanged,
        urlChanged: searchParams.toString() !== currentSearchParams.toString()
    };
}

export function debounce(fn: () => void, delay: false | number): DebouncedFunction {
    let timeout: ReturnType<typeof setTimeout> | undefined;

    const cancel = () => {
        if (timeout !== undefined) {
            clearTimeout(timeout);
            timeout = undefined;
        }
    };

    const debounced = () => {
        cancel();

        if (delay === false || delay <= 0) {
            fn();
            return;
        }

        timeout = setTimeout(() => {
            timeout = undefined;
            fn();
        }, delay);
    };

    debounced.cancel = cancel;
    return debounced;
}

export function parseURL<T extends Schema>(searchParams: URLSearchParams, schema: T, defaults?: Partial<QueryParamValues<T>>): SchemaOutput<T> {
    const result = {} as SchemaOutput<T>;
    const resultValues = result as Record<string, PrimitiveValue>;
    const defaultValues = defaults as Partial<Record<string, PrimitiveValue | undefined>> | undefined;

    for (const key of Object.keys(schema)) {
        const primitive = schema[key];
        if (!primitive) {
            continue;
        }

        const fallback = coercePrimitive(primitive, defaultValues?.[key]);
        resultValues[key] = coercePrimitive(primitive, searchParams.get(key), fallback);
    }

    return result;
}

export function resetQueryParams<T extends Schema>(
    currentState: SchemaOutput<T>,
    currentSearchParams: URLSearchParams,
    schema: T,
    defaults?: Partial<QueryParamValues<T>>
): QueryParamUpdate<T> {
    const searchParams = new URLSearchParams(currentSearchParams);
    for (const key of Object.keys(schema)) {
        searchParams.delete(key);
    }

    const state = parseURL(searchParams, schema, defaults);

    return {
        searchParams,
        state,
        stateChanged: Object.keys(schema).some((key) => !valuesEqual(currentState[key], state[key])),
        urlChanged: searchParams.toString() !== currentSearchParams.toString()
    };
}

function setSearchParam(searchParams: URLSearchParams, key: string, value: null | string) {
    if (value === null || value === '') {
        searchParams.delete(key);
    } else {
        searchParams.set(key, value);
    }
}

function stringifyPrimitive(primitiveType: Primitive, value: PrimitiveValue): null | string {
    switch (primitiveType) {
        case 'boolean': {
            return value === null ? null : value ? 'true' : 'false';
        }

        case 'date': {
            return value instanceof Date && !Number.isNaN(value.getTime()) ? value.toISOString() : null;
        }

        case 'number': {
            return typeof value === 'number' && Number.isFinite(value) ? String(value) : null;
        }

        case 'string': {
            return typeof value === 'string' && value ? value : null;
        }

        default: {
            return typeof value === 'string' && value ? value : null;
        }
    }
}

function valuesEqual(current: unknown, next: unknown): boolean {
    if (current instanceof Date && next instanceof Date) {
        return current.getTime() === next.getTime();
    }

    return Object.is(current, next);
}
