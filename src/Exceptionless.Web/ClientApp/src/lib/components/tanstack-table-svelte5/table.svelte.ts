import type { Updater } from '@tanstack/svelte-table';
import { createTable, type RowData, type TableOptions, type TableOptionsResolved, type TableState } from '@tanstack/table-core';

export function createSvelteTable<TData extends RowData>(options: TableOptions<TData>) {
    const resolvedOptions: TableOptionsResolved<TData> = mergeObjects(
        {
            state: {},
            onStateChange() {},
            renderFallbackValue: null,
            mergeOptions: (defaultOptions: TableOptions<TData>, options: Partial<TableOptions<TData>>) => {
                return mergeObjects(defaultOptions, options);
            }
        },
        options
    );

    const table = createTable(resolvedOptions);
    let state = $state<Partial<TableState>>(table.initialState);

    function updateOptions() {
        table.setOptions((prev) => {
            return mergeObjects(prev, options, {
                state: mergeObjects(state, options.state || {}),
                onStateChange: (updater: Updater<TableState>) => {
                    if (updater instanceof Function) state = updater(state as TableState);
                    else state = mergeObjects(state, updater);

                    options.onStateChange?.(updater);
                }
            });
        });
    }

    updateOptions();

    $effect.pre(() => {
        updateOptions();
    });

    return table;
}

/**
 * Merges objects together while keeping their getters alive.
 * Taken from SolidJS: {@link https://github.com/solidjs/solid/blob/24abc825c0996fd2bc8c1de1491efe9a7e743aff/packages/solid/src/server/rendering.ts#L82-L115}
 * */
export function mergeObjects<T>(source: T): T;
export function mergeObjects<T, U>(source: T, source1: U): T & U;
export function mergeObjects<T, U, V>(source: T, source1: U, source2: V): T & U & V;
export function mergeObjects<T, U, V, W>(source: T, source1: U, source2: V, source3: W): T & U & V & W;
export function mergeObjects(...sources: unknown[]): Record<PropertyKey, unknown> {
    const target = {};
    for (let i = 0; i < sources.length; i++) {
        let source = sources[i];
        if (typeof source === 'function') source = source();
        if (source) {
            const descriptors = Object.getOwnPropertyDescriptors(source);
            for (const key in descriptors) {
                if (key in target) continue;
                Object.defineProperty(target, key, {
                    enumerable: true,
                    get() {
                        for (let i = sources.length - 1; i >= 0; i--) {
                            let s = sources[i];
                            if (typeof s === 'function') s = s();
                            const v = ((s || {}) as Record<PropertyKey, unknown>)[key];
                            if (v !== undefined) return v;
                        }
                    }
                });
            }
        }
    }
    return target;
}
