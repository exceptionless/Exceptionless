import type { OutputOfPrimitive, Primitive } from './types.js';

export function coercePrimitive<T extends Primitive>(primitiveType: T, value: unknown, fallback: OutputOfPrimitive<T> = null as OutputOfPrimitive<T>) {
    if (value === undefined || value === null || value === '' || value === 'null') {
        return fallback;
    }

    switch (primitiveType) {
        case 'boolean': {
            if (typeof value === 'boolean') {
                return value as OutputOfPrimitive<T>;
            }

            if (value === 1 || value === '1' || (typeof value === 'string' && value.toLowerCase() === 'true')) {
                return true as OutputOfPrimitive<T>;
            }

            if (value === 0 || value === '0' || (typeof value === 'string' && value.toLowerCase() === 'false')) {
                return false as OutputOfPrimitive<T>;
            }

            return fallback;
        }

        case 'date': {
            const date = value instanceof Date ? value : new Date(value === '0' ? 0 : String(value));

            return (Number.isNaN(date.getTime()) ? fallback : date) as OutputOfPrimitive<T>;
        }

        case 'number': {
            const number = typeof value === 'number' ? value : Number(value);

            return (Number.isFinite(number) ? number : fallback) as OutputOfPrimitive<T>;
        }

        case 'string': {
            return String(value) as OutputOfPrimitive<T>;
        }

        default: {
            return (validateEnum(primitiveType, value) ? value : fallback) as OutputOfPrimitive<T>;
        }
    }
}

function validateEnum(enumType: string, value: unknown): value is string {
    if (typeof value !== 'string' || !value) {
        return false;
    }

    return enumType.slice(1, -1).split(',').includes(value);
}
