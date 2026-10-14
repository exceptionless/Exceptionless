import type { QueryParameterScalarType, QueryParameterScalarValue, QueryParameterType, QueryParameterTypeOutput } from './types.ts';

const emptyList: readonly never[] = Object.freeze([]);

export function coerceQueryParameter<T extends QueryParameterType>(
    parameterType: T,
    value: unknown,
    fallback?: QueryParameterTypeOutput<T>
): QueryParameterTypeOutput<T> {
    const itemType = getListItemType(parameterType);
    if (itemType) {
        return coerceQueryParameterList(itemType, value, fallback as readonly QueryParameterScalarValue[] | undefined) as QueryParameterTypeOutput<T>;
    }

    return coerceScalar(parameterType as QueryParameterScalarType, value, (fallback ?? null) as QueryParameterScalarValue) as QueryParameterTypeOutput<T>;
}

/** Returns the item type of a list parameter such as `'number[]'`, or `undefined` for scalar parameters. */
export function getListItemType(parameterType: QueryParameterType): QueryParameterScalarType | undefined {
    return parameterType.endsWith('[]') ? (parameterType.slice(0, -2) as QueryParameterScalarType) : undefined;
}

function coerceQueryParameterList(
    itemType: QueryParameterScalarType,
    value: unknown,
    fallback: readonly QueryParameterScalarValue[] = emptyList
): readonly QueryParameterScalarValue[] {
    if (value === undefined || value === null) {
        return fallback;
    }

    const items: QueryParameterScalarValue[] = [];
    const seen = new Set<unknown>();
    for (const item of Array.isArray(value) ? value : [value]) {
        // Empty strings mark an explicitly empty list in the URL; they are never list items.
        const coerced = item === '' ? null : coerceScalar(itemType, item, null);
        const identity = coerced instanceof Date ? coerced.getTime() : coerced;
        if (coerced !== null && !seen.has(identity)) {
            seen.add(identity);
            items.push(coerced);
        }
    }

    return items.length > 0 ? Object.freeze(items) : emptyList;
}

function coerceScalar(parameterType: QueryParameterScalarType, value: unknown, fallback: QueryParameterScalarValue): QueryParameterScalarValue {
    if (value === undefined || value === null || value === 'null' || (value === '' && parameterType !== 'string')) {
        return fallback;
    }

    switch (parameterType) {
        case 'boolean': {
            if (typeof value === 'boolean') {
                return value;
            }

            if (value === 1 || value === '1' || (typeof value === 'string' && value.toLowerCase() === 'true')) {
                return true;
            }

            if (value === 0 || value === '0' || (typeof value === 'string' && value.toLowerCase() === 'false')) {
                return false;
            }

            return fallback;
        }

        case 'date': {
            const date = value instanceof Date ? value : new Date(value === '0' ? 0 : String(value));
            return Number.isNaN(date.getTime()) ? fallback : date;
        }

        case 'number': {
            const number = typeof value === 'number' ? value : Number(value);
            return Number.isFinite(number) ? number : fallback;
        }

        case 'string': {
            return String(value);
        }

        default: {
            return validateEnum(parameterType, value) ? value : fallback;
        }
    }
}

function validateEnum(parameterType: string, value: unknown): value is string {
    if (typeof value !== 'string' || !value) {
        return false;
    }

    return parameterType.slice(1, -1).split(',').includes(value);
}
