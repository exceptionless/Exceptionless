import type { QueryParameterInput, QueryParameterSchema, QueryParameterState, QueryParameterType, QueryParameterValue } from './types.js';

import { coerceQueryParameter } from './coerce.js';

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
    schema: T
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
        setSearchParameter(searchParams, key, stringifyQueryParameter(parameterType, coerced));
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
        resultValues[key] = coerceQueryParameter(parameterType, searchParams.get(key), fallback);
    }

    return result;
}

export function searchParamsEqual(left: URLSearchParams, right: string | URLSearchParams): boolean {
    return left.toString() === createSearchParams(right).toString();
}

function queryParameterValuesEqual(current: unknown, next: unknown): boolean {
    if (current instanceof Date && next instanceof Date) {
        return current.getTime() === next.getTime();
    }

    return Object.is(current, next);
}

function setSearchParameter(searchParams: URLSearchParams, key: string, value: null | string): void {
    if (value === null) {
        searchParams.delete(key);
    } else {
        searchParams.set(key, value);
    }
}

function stringifyQueryParameter(parameterType: QueryParameterType, value: QueryParameterValue): null | string {
    switch (parameterType) {
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
            return typeof value === 'string' ? value : null;
        }

        default: {
            return typeof value === 'string' && value ? value : null;
        }
    }
}
