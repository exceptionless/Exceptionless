import type { IFilter } from './models';

export const filterScopeKey = Symbol('filter-scope');

export interface FilterScope {
    readonly filters: IFilter[];
    readonly time?: null | string;
}
