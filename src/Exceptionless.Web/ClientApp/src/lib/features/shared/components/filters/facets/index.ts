import type { Component } from 'svelte';

import {
    type BooleanFilter,
    type DateFilter,
    type IFilter,
    KeywordFilter,
    type NumberFilter,
    OrganizationFilter,
    ProjectFilter,
    ReferenceFilter,
    SessionFilter,
    StatusFilter,
    type StringFilter,
    TypeFilter,
    type VersionFilter
} from '../filters.svelte';
import BooleanFacetedFilter from './BooleanFacetedFilter.svelte';
import DateFacetedFilter from './DateFacetedFilter.svelte';
import KeywordFacetedFilter from './KeywordFacetedFilter.svelte';
import NumberFacetedFilter from './NumberFacetedFilter.svelte';
import OrganizationFacetedFilter from './OrganizationFacetedFilter.svelte';
import ProjectFacetedFilter from './ProjectFacetedFilter.svelte';
import ReferenceFacetedFilter from './ReferenceFacetedFilter.svelte';
import SessionFacetedFilter from './SessionFacetedFilter.svelte';
import StatusFacetedFilter from './StatusFacetedFilter.svelte';
import StringFacetedFilter from './StringFacetedFilter.svelte';
import TypeFacetedFilter from './TypeFacetedFilter.svelte';
import VersionFacetedFilter from './VersionFacetedFilter.svelte';

export type FacetedFilterProps<TFilter extends IFilter> = {
    filter: TFilter;
    filterChanged: (filter: TFilter) => void;
    filterRemoved: (filter: TFilter) => void;
    open: boolean;
    title: string;
};

export class FacetedFilter<TFilter extends IFilter> {
    constructor(
        public title: string,
        public component: Component<FacetedFilterProps<TFilter>>,
        public filter: TFilter,
        public open: boolean = false
    ) {}
}

export function toFacetedFilters(filters: IFilter[]): FacetedFilter<IFilter>[] {
    return filters.map((filter) => {
        switch (filter.type) {
            case 'boolean': {
                const booleanFilter = filter as BooleanFilter;
                return new FacetedFilter((booleanFilter.term as string) ?? 'Boolean', BooleanFacetedFilter, booleanFilter);
            }
            case 'date': {
                const dateFilter = filter as DateFilter;
                const title = dateFilter.term === 'date' ? 'Date Range' : (dateFilter.term ?? 'Date');
                return new FacetedFilter(title, DateFacetedFilter, dateFilter);
            }
            case 'keyword': {
                return new FacetedFilter('Keyword', KeywordFacetedFilter, filter as KeywordFilter);
            }
            case 'number': {
                const numberFilter = filter as NumberFilter;
                return new FacetedFilter((numberFilter.term as string) ?? 'Number', NumberFacetedFilter, numberFilter);
            }
            case 'organization': {
                return new FacetedFilter('Organization', OrganizationFacetedFilter, filter as OrganizationFilter);
            }
            case 'project': {
                return new FacetedFilter('Project', ProjectFacetedFilter, filter as ProjectFilter);
            }
            case 'reference': {
                return new FacetedFilter('Reference', ReferenceFacetedFilter, filter as ReferenceFilter);
            }
            case 'session': {
                return new FacetedFilter('Session', SessionFacetedFilter, filter as SessionFilter);
            }
            case 'status': {
                return new FacetedFilter('Status', StatusFacetedFilter, filter as StatusFilter);
            }
            case 'string': {
                const stringFilter = filter as StringFilter;
                return new FacetedFilter((stringFilter.term as string) ?? 'String', StringFacetedFilter, stringFilter);
            }
            case 'type': {
                return new FacetedFilter('Type', TypeFacetedFilter, filter as TypeFilter);
            }
            case 'version': {
                const versionFilter = filter as VersionFilter;
                return new FacetedFilter((versionFilter.term as string) ?? 'Version', VersionFacetedFilter, versionFilter);
            }
            default: {
                throw new Error(`Unknown filter type: ${filter.type}`);
            }
        }
    }) as unknown as FacetedFilter<IFilter>[];
    // TODO: look into why unknown is required here.
}
