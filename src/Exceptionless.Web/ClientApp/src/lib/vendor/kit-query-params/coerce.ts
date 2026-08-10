import type { OutputOfPrimitive, Primitive, Schema, SchemaOutput } from './types.js';

export const validateEnum = (enumType: string, value: null | string | undefined) => {
    if (!value) {
        return false;
    }

    const types = enumType.replace('<', '').replace('>', '').split(',');
    return types.includes(value);
};

export const coerceArray = (schema: Schema, value: any[] | null, DEFAULT_VALUE: any = null): SchemaOutput<Schema>[] => {
    if (!value) {
        return DEFAULT_VALUE;
    }

    return value.map((v, index) => coerceObject(schema, v, DEFAULT_VALUE?.[index]));
};

export const coerceObject = (schema: Schema, value: any, DEFAULT_VALUE: any = null): SchemaOutput<Schema> => {
    if (!value) {
        return DEFAULT_VALUE;
    }

    const result: Record<string, any> = {};
    for (const [key, schemaType] of Object.entries(schema)) {
        if (typeof schemaType === 'string') {
            // Handle primitive types
            result[key] = coercePrimitive(schemaType as Primitive, value[key], DEFAULT_VALUE?.[key]);
        } else if (Array.isArray(schemaType)) {
            // Handle array types
            const arraySchema = schemaType[0];
            if (typeof arraySchema === 'string') {
                result[key] = coercePrimitiveArray(arraySchema as Primitive, value[key], DEFAULT_VALUE?.[key]);
            } else {
                result[key] = coerceArray(arraySchema, value[key], DEFAULT_VALUE?.[key]);
            }
        } else if (typeof schemaType === 'object') {
            // Handle nested object types
            result[key] = coerceObject(schemaType, value[key], DEFAULT_VALUE?.[key]);
        }
    }

    return result as SchemaOutput<Schema>;
};

export const coercePrimitiveArray = (primitiveType: Primitive, value: null | string[], DEFAULT_VALUE: any = null): OutputOfPrimitive<Primitive>[] => {
    if (!value) {
        return DEFAULT_VALUE;
    }

    return value.map((v, index) => coercePrimitive(primitiveType, v, DEFAULT_VALUE?.[index])).filter((v) => v !== null);
};

export const coercePrimitive = (primitiveType: Primitive, value: null | string | undefined, DEFAULT_VALUE: any = null): OutputOfPrimitive<Primitive> => {
    if (value === 'null' || value === '' || value === null) {
        return DEFAULT_VALUE;
    }

    switch (primitiveType) {
        case 'boolean': {
            if (value === null || value === undefined) {
                return DEFAULT_VALUE;
            }

            if (typeof value === 'boolean') {
                return value;
            }

            if (Number(value) === 1) {
                return true;
            }

            if (Number(value) === 0) {
                return false;
            }

            if (typeof value === 'string') {
                return value.toLowerCase() === 'true' ? true : value.toLowerCase() === 'false' ? false : DEFAULT_VALUE;
            }

            return DEFAULT_VALUE;
        }

        case 'date': {
            return value ? (isNaN(new Date(value).getTime()) ? DEFAULT_VALUE : new Date(value === '0' ? 0 : value)) : DEFAULT_VALUE;
        }

        case 'number': {
            const parsed = Number(value);
            if ((!value || isNaN(parsed)) && parsed !== 0) {
                return DEFAULT_VALUE;
            }

            return parsed;
        }

        case 'string': {
            return value && value.toString ? value.toString() : DEFAULT_VALUE;
        }

        default: {
            // Assume it is an enum
            return validateEnum(primitiveType, value) ? value! : DEFAULT_VALUE;
        }
    }
};
