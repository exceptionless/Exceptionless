type Primitive = string | null | symbol | boolean | number | undefined | bigint;

function is_primitive(value: unknown): value is Primitive {
    return value !== Object(value) || value === null;
}

// NOTE: This is a hack until we can upgrade svelte-persisted-store to runes.
// https://twitter.com/puruvjdev/status/1787037268143689894/photo/1
// https://github.com/joshnuss/svelte-persisted-store/discussions/251
// TODO: Have this support serialization.
export function persisted<T>(key: string, initial: T) {
    const existing = localStorage.getItem(key);

    const primitive = is_primitive(initial);
    const parsed_value = existing ? JSON.parse(existing) : initial;

    const state = $state<T extends Primitive ? { value: T } : T>(primitive ? { value: parsed_value } : parsed_value);

    $effect.root(() => {
        $effect(() => {
            localStorage.setItem(key, JSON.stringify(primitive ? (state as { value: T }).value : state));
        });
    });

    return state;
}
