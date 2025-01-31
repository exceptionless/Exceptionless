import type { Component } from 'svelte';

import { SvelteMap } from 'svelte/reactivity';

import type { FacetedFilterProps, IFilter } from './models';

export interface FacetFilterBuilder<TFilter extends IFilter> {
    component: Component<FacetedFilterProps<TFilter>>;
    create: (filter?: TFilter) => TFilter;
    priority: number;
    title: string;
}

export const builderContext = $state(new SvelteMap<string, FacetFilterBuilder<IFilter>>());
