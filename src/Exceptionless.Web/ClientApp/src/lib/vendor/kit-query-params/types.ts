export type OutputOfPrimitive<T extends Primitive> = T extends 'string'
    ? null | string
    : T extends 'number'
      ? null | number
      : T extends 'date'
        ? Date | null
        : T extends 'boolean'
          ? boolean | null
          : InferEnum<T>;

export type Primitive = 'boolean' | 'date' | 'number' | 'string' | `<${string}>`;

export type PrimitiveValue = boolean | Date | null | number | string;

export type QueryParamsOptions<T extends Schema> = {
    debounce?: false | number;
    default?: Partial<QueryParamValues<T>>;
    pushHistory?: boolean;
    schema: T;
};

export type QueryParamsState<T extends Schema> = SchemaOutput<T> & {
    reset: () => void;
    toURLSearchParams: () => URLSearchParams;
    update: (values: Partial<QueryParamValues<T>>) => void;
};

export type QueryParamValues<T extends Schema> = {
    [K in keyof T]: OutputOfPrimitive<T[K]> | undefined;
};

export type Schema = Record<string, Primitive>;

export type SchemaOutput<T extends Schema> = {
    [K in keyof T]: OutputOfPrimitive<T[K]>;
};

type InferEnum<T> = T extends `<${infer U}>` ? (U extends `${infer First},${infer Rest}` ? First | InferEnum<`<${Rest}>`> : null | U) : never;
