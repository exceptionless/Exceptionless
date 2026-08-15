export interface CreateQueryParametersOptions<T extends QueryParameterSchema> {
    debounceMilliseconds?: number;
    defaults?: Partial<QueryParameterInput<T>>;
    history?: 'push' | 'replace';
    schema: T;
}

export type QueryParameterInput<T extends QueryParameterSchema> = {
    [K in keyof T]: QueryParameterTypeOutput<T[K]> | undefined;
};

export type QueryParameters<T extends QueryParameterSchema> = QueryParameterState<T> & {
    update: (values: Partial<QueryParameterInput<T>>) => void;
};

export type QueryParameterSchema = Record<string, QueryParameterType>;

export type QueryParameterState<T extends QueryParameterSchema> = {
    [K in keyof T]: QueryParameterTypeOutput<T[K]>;
};

export type QueryParameterType = 'boolean' | 'date' | 'number' | 'string' | `<${string}>`;

export type QueryParameterTypeOutput<T extends QueryParameterType> = T extends 'string'
    ? null | string
    : T extends 'number'
      ? null | number
      : T extends 'date'
        ? Date | null
        : T extends 'boolean'
          ? boolean | null
          : InferEnum<T>;

export type QueryParameterValue = boolean | Date | null | number | string;

type InferEnum<T> = T extends `<${infer U}>` ? (U extends `${infer First},${infer Rest}` ? First | InferEnum<`<${Rest}>`> : null | U) : never;
