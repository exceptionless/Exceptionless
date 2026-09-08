import type { QueryParameterInput, QueryParameters, QueryParameterSchema, QueryParameterState, QueryParameterUpdateOptions } from './types.js';

interface QueryParameterActions<T extends QueryParameterSchema> {
    update: (values: Partial<QueryParameterInput<T>>, options?: QueryParameterUpdateOptions) => void;
}

export function createQueryParameterProxy<T extends QueryParameterSchema>(
    state: QueryParameterState<T>,
    schema: T,
    actions: QueryParameterActions<T>
): QueryParameters<T> {
    const handler: ProxyHandler<QueryParameterState<T>> = {
        get(target, property, receiver) {
            if (property === 'update') {
                return actions.update;
            }

            return Reflect.get(target, property, receiver);
        },
        set(_target, property, value) {
            if (typeof property === 'string' && Object.hasOwn(schema, property)) {
                actions.update({ [property]: value } as Partial<QueryParameterInput<T>>);
                return true;
            }

            return true;
        }
    };

    return new Proxy(state, handler) as QueryParameters<T>;
}
