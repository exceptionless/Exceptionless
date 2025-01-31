import type { Component } from 'svelte';

export type FacetedFilterProps<TFilter extends IFilter> = {
    filter: TFilter;
    filterChanged: (filter: TFilter) => void;
    filterRemoved: (filter: TFilter) => void;
    open: boolean;
    title: string;
};

export interface IFilter {
    readonly id: string;
    isEmpty(): boolean;
    readonly key: string;
    reset(): void;
    toFilter(): string;
    readonly type: string;
}

export class FacetedFilter<TFilter extends IFilter> {
    constructor(
        public title: string,
        public component: Component<FacetedFilterProps<TFilter>>,
        public filter: TFilter,
        public open: boolean = false
    ) {}
}
