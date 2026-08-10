// Adapted from type-fest SimplifyDeep
export type ConditionalSimplifyDeep<Type, ExcludeType = never, IncludeType = unknown> = Type extends ExcludeType
    ? Type
    : Type extends IncludeType
      ? { [TypeKey in keyof Type]: ConditionalSimplifyDeep<Type[TypeKey], ExcludeType, IncludeType> }
      : Type;
export type Default<T extends Schema> = {
    [K in keyof T]?: T[K] extends Primitive
        ? NonNullable<OutputOfPrimitive<T[K]>>
        : T[K] extends Schema
          ? Default<T[K]>
          : T[K] extends [Schema]
            ? Default<T[K][number]>[]
            : T[K] extends [Primitive]
              ? NonNullable<OutputOfPrimitive<T[K][0]>>[]
              : never;
};

export type NonRecursiveType = bigint | boolean | Function | (new (...arguments_: any[]) => unknown) | null | number | string | symbol | undefined;

export type Opts<S extends Schema, D extends Default<S> | undefined, Enforce extends boolean = false> = {
    debounce?: false | number;
    default?: D;
    enforceDefault?: Enforce;
    invalidate?: (string | URL)[];
    invalidateAll?: boolean;
    preserveUnknownParams?: boolean;
    pushHistory?: boolean;
    schema: S;
    shallow?: boolean;
    twoWayBinding?: boolean;
};

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

export type PrimitiveSchema = Record<string, Primitive>;

export type Schema = {
    [key: string]: [Primitive] | [Schema] | Primitive | Schema;
};

export type SchemaOutput<T extends Schema, D = undefined, Enforce extends boolean = false> = {
    [K in keyof T]: T[K] extends Primitive
        ? MaybeNotNullable<OutputOfPrimitive<T[K]>, Get<D, K>, Enforce>
        : T[K] extends Schema
          ? SchemaOutput<T[K], Get<D, K>>
          : T[K] extends [Schema]
            ? SchemaOutput<T[K][number], Get<D, K>, Enforce>[]
            : T[K] extends [Primitive]
              ? MaybeNotNullable<Exclude<OutputOfPrimitive<T[K][number]>, null>, D, Enforce>[]
              : never;
};

export type Simplify<Type, ExcludeType = never> = ConditionalSimplifyDeep<Type, ExcludeType | Map<unknown, unknown> | NonRecursiveType | Set<unknown>, object>;
type Get<T, K> = K extends keyof T ? T[K] : undefined;

type InferEnum<T> = T extends `<${infer U}>` ? (U extends `${infer First},${infer Rest}` ? First | InferEnum<`<${Rest}>`> : null | U) : never;

type MaybeNotNullable<T, D, Enforce extends boolean> = D extends undefined ? T : Enforce extends true ? Exclude<T, null> : T;
