export interface CreateQueryParametersOptions<T extends QueryParameterSchema> {
    debounceMilliseconds?: number;
    defaults?: Partial<QueryParameterInput<T>>;
    history?: 'push' | 'replace';
    schema: T;
}

export type QueryParameterInput<T extends QueryParameterSchema> = {
    [K in keyof T]: QueryParameterTypeOutput<T[K]> | undefined;
};

export type QueryParameterListType = `${QueryParameterScalarType}[]`;

export type QueryParameters<T extends QueryParameterSchema> = QueryParameterState<T> & {
    update: (values: Partial<QueryParameterInput<T>>, options?: QueryParameterUpdateOptions) => void;
};

export type QueryParameterScalarType = 'boolean' | 'date' | 'number' | 'string' | `<${string}>`;

export type QueryParameterScalarValue = boolean | Date | null | number | string;

export type QueryParameterSchema = Record<string, QueryParameterType>;

export type QueryParameterState<T extends QueryParameterSchema> = {
    [K in keyof T]: QueryParameterTypeOutput<T[K]>;
};

export type QueryParameterType = QueryParameterListType | QueryParameterScalarType;

/**
 * List parameters are never null. They are frozen because the proxy only observes
 * top-level assignment; an in-place mutation would change state without the URL.
 */
export type QueryParameterTypeOutput<T extends QueryParameterType> = T extends `${infer Item extends QueryParameterScalarType}[]`
    ? readonly NonNullable<QueryParameterScalarOutput<Item>>[]
    : T extends QueryParameterScalarType
      ? QueryParameterScalarOutput<T>
      : never;

export interface QueryParameterUpdateOptions {
    history?: 'push' | 'replace';
}

export type QueryParameterValue = QueryParameterScalarValue | readonly QueryParameterScalarValue[];

type InferEnum<T> = T extends `<${infer U}>` ? (U extends `${infer First},${infer Rest}` ? First | InferEnum<`<${Rest}>`> : null | U) : never;

type QueryParameterScalarOutput<T extends QueryParameterScalarType> = T extends 'string'
    ? null | string
    : T extends 'number'
      ? null | number
      : T extends 'date'
        ? Date | null
        : T extends 'boolean'
          ? boolean | null
          : InferEnum<T>;
