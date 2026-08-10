import type { SvelteURLSearchParams } from 'svelte/reactivity';

import type { Default, Schema, SchemaOutput, Simplify } from './types.js';

import { coerceArray, coerceObject, coercePrimitive, coercePrimitiveArray } from './coerce.js';
import { traverseSchema } from './traverse.js';
import { isPrimitive, stringifyPrimitive } from './utils.js';

function valuesEqual(current: unknown, next: unknown): boolean {
    if (current instanceof Date && next instanceof Date) {
        return current.getTime() === next.getTime();
    }

    return Object.is(current, next);
}

export const createProxy = <T extends Schema, D extends Default<T> | undefined, Enforce extends boolean = false>(
    obj: any,
    {
        array,
        clearPaths = () => {},
        default: defaultValue,
        enforceDefault,
        onUpdate,
        path = '',
        reset,
        schema,
        searchParams,
        sync
    }: {
        array?: any[];
        clearPaths?: (path: string) => void;
        default?: any;
        enforceDefault?: boolean;
        onUpdate: (path: string, value: any) => void;
        path?: string;
        reset: () => void;
        schema: T;
        searchParams: SvelteURLSearchParams | URLSearchParams;
        sync: () => void;
    }
) => {
    const handler: ProxyHandler<SchemaOutput<T>> = {
        get(target: SchemaOutput<T>, key: string) {
            if (key === '$searchParams') {
                return searchParams;
            }

            if (key === '$reset') {
                return reset;
            }

            if (key === '$sync') {
                return sync;
            }

            const value = Reflect.get(target, key);

            if (array) {
                // Handle array mutation methods
                if (typeof key === 'string' && ['pop', 'push', 'reverse', 'shift', 'sort', 'splice', 'unshift'].includes(key)) {
                    return function (this: any[], ...args: any[]) {
                        const result = Array.prototype[key as keyof typeof Array.prototype].apply(this, args);

                        // Handle array length changes
                        if (key === 'pop' || key === 'shift') {
                            const index = this.length;
                            clearPaths(`${path}.${index - 1}`);
                            Reflect.set(target, 'length', index - 1);
                        }

                        return result;
                    };
                }

                // Handle array read methods
                if (
                    typeof key === 'string' &&
                    [
                        'at',
                        'concat',
                        'entries',
                        'every',
                        'filter',
                        'find',
                        'findIndex',
                        'findLast',
                        'findLastIndex',
                        'flat',
                        'flatMap',
                        'forEach',
                        'includes',
                        'indexOf',
                        'join',
                        'keys',
                        'lastIndexOf',
                        'map',
                        'reduce',
                        'reduceRight',
                        'slice',
                        'some',
                        'toLocaleString',
                        'toString',
                        'values'
                    ].includes(key)
                ) {
                    return function (...args: any[]) {
                        return Array.prototype[key as keyof typeof Array.prototype].apply(target, args);
                    };
                }
            }

            if (typeof value === 'object' && value !== null && !(value instanceof Date)) {
                return createProxy(value, {
                    array: Array.isArray(value) ? value : undefined,
                    clearPaths,
                    default: defaultValue?.[key as keyof T],
                    onUpdate,
                    path: path ? `${path}.${key}` : key,
                    reset,
                    schema: schema?.[key as keyof T] as Schema,
                    searchParams,
                    sync
                });
            }

            return value;
        },
        set(target: SchemaOutput<T>, prop: string, value: any) {
            const isArrayTargeted = Array.isArray(target);
            const isLengthTargeted = isArrayTargeted && prop === 'length';
            const schemaType = isArrayTargeted ? schema[0] : schema[prop];
            const isArray = Array.isArray(schemaType);
            const type = isArray ? schemaType[0] : schemaType;
            const primitive = isPrimitive(type) ? type : undefined;
            const objectSchema = isPrimitive(type) ? undefined : type;
            const basePath = path ? `${path}.${prop}` : prop;
            if (isLengthTargeted) {
                return true;
            }

            if (valuesEqual(Reflect.get(target, prop), value)) {
                return true;
            }

            // TODO when reassining array or object we should cleanup all previous paths
            if (objectSchema) {
                if (isArray || isLengthTargeted) {
                    const parsed = coerceArray(objectSchema, value, enforceDefault && defaultValue?.[prop]);

                    // clearPaths(`${basePath}`);
                    parsed.forEach((v, i) => {
                        traverseSchema({
                            cb: ({ follower, path, primitive }) => {
                                onUpdate(`${basePath}.${i}.${path}`, stringifyPrimitive(primitive, follower));
                            },
                            follower: v,
                            schema: objectSchema
                        });
                    });

                    Reflect.set(target, prop, parsed);
                } else {
                    const parsed = coerceObject(objectSchema, value, enforceDefault && defaultValue?.[prop]);
                    clearPaths(`${basePath}`);
                    traverseSchema({
                        cb: ({ follower, path, primitive }) => {
                            onUpdate(`${basePath}.${path}`, stringifyPrimitive(primitive, follower));
                        },
                        follower: parsed,
                        schema: objectSchema
                    });
                    Reflect.set(target, prop, parsed);
                }
            } else if (primitive) {
                if (isArray) {
                    const parsed = coercePrimitiveArray(primitive, value, enforceDefault && defaultValue?.[prop]);
                    clearPaths(`${basePath}`);
                    parsed.forEach((v, i) => {
                        onUpdate(`${basePath}.${i}`, stringifyPrimitive(primitive, v));
                    });

                    Reflect.set(target, prop, parsed);
                } else {
                    const parsed = coercePrimitive(primitive, value, enforceDefault && defaultValue?.[prop]);
                    if (parsed === null && !isNaN(Number(prop))) {
                        // we avoid pushing null values to the array
                        return true;
                    }

                    if (valuesEqual(Reflect.get(target, prop), parsed)) {
                        return true;
                    }

                    onUpdate(basePath, stringifyPrimitive(primitive, parsed));
                    Reflect.set(target, prop, parsed);
                }
            }

            return true;
        }
    };

    return new Proxy(obj, handler) as Simplify<SchemaOutput<T, D, Enforce>> & {
        $reset: (enforceDefault?: boolean) => void;
        $searchParams: SvelteURLSearchParams;
        $sync: () => void;
    };
};
