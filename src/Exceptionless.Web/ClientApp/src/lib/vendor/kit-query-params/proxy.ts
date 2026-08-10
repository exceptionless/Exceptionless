import type { QueryParamsState, QueryParamValues, Schema, SchemaOutput } from './types.js';

interface QueryParamsActions<T extends Schema> {
    reset: () => void;
    toURLSearchParams: () => URLSearchParams;
    update: (values: Partial<QueryParamValues<T>>) => void;
}

export function createProxy<T extends Schema>(state: SchemaOutput<T>, schema: T, actions: QueryParamsActions<T>): QueryParamsState<T> {
    const handler: ProxyHandler<SchemaOutput<T>> = {
        get(target, property, receiver) {
            if (property === 'reset') {
                return actions.reset;
            }

            if (property === 'toURLSearchParams') {
                return actions.toURLSearchParams;
            }

            if (property === 'update') {
                return actions.update;
            }

            return Reflect.get(target, property, receiver);
        },
        set(_target, property, value) {
            if (typeof property === 'string' && Object.hasOwn(schema, property)) {
                actions.update({ [property]: value } as Partial<QueryParamValues<T>>);
                return true;
            }

            return false;
        }
    };

    return new Proxy(state, handler) as QueryParamsState<T>;
}
