import type { QueryParameterType, QueryParameterTypeOutput } from './types.js';

export function coerceQueryParameter<T extends QueryParameterType>(
    parameterType: T,
    value: unknown,
    fallback: QueryParameterTypeOutput<T> = null as QueryParameterTypeOutput<T>
): QueryParameterTypeOutput<T> {
    if (value === undefined || value === null || value === 'null' || (value === '' && parameterType !== 'string')) {
        return fallback;
    }

    switch (parameterType) {
        case 'boolean': {
            if (typeof value === 'boolean') {
                return value as QueryParameterTypeOutput<T>;
            }

            if (value === 1 || value === '1' || (typeof value === 'string' && value.toLowerCase() === 'true')) {
                return true as QueryParameterTypeOutput<T>;
            }

            if (value === 0 || value === '0' || (typeof value === 'string' && value.toLowerCase() === 'false')) {
                return false as QueryParameterTypeOutput<T>;
            }

            return fallback;
        }

        case 'date': {
            const date = value instanceof Date ? value : new Date(value === '0' ? 0 : String(value));
            return (Number.isNaN(date.getTime()) ? fallback : date) as QueryParameterTypeOutput<T>;
        }

        case 'number': {
            const number = typeof value === 'number' ? value : Number(value);
            return (Number.isFinite(number) ? number : fallback) as QueryParameterTypeOutput<T>;
        }

        case 'string': {
            return String(value) as QueryParameterTypeOutput<T>;
        }

        default: {
            return (validateEnum(parameterType, value) ? value : fallback) as QueryParameterTypeOutput<T>;
        }
    }
}

function validateEnum(parameterType: string, value: unknown): value is string {
    if (typeof value !== 'string' || !value) {
        return false;
    }

    return parameterType.slice(1, -1).split(',').includes(value);
}
