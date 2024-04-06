import type { FacetedFilter } from '$comp/faceted-filter';
import { writable } from 'svelte/store';
import { type BooleanFilter, type DateFilter, type IFilter, type NumberFilter, type StringFilter, type VersionFilter } from '../filters';

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

export function toFacetedFilters(filters: IFilter[]): FacetedFilter[] {
    return filters.map((filter) => {
        switch (filter.type) {
            case 'boolean': {
                const booleanFilter = filter as BooleanFilter;
                return { title: (booleanFilter.term as string) ?? 'Boolean', component: BooleanFacetedFilter, filter, open: writable(false) };
            }
            case 'date': {
                const dateFilter = filter as DateFilter;
                const title = dateFilter.term === 'date' ? 'Date Range' : dateFilter.term ?? 'Date';
                return { title, component: DateFacetedFilter, filter, open: writable(false) };
            }
            case 'keyword': {
                return { title: 'Keyword', component: KeywordFacetedFilter, filter, open: writable(false) };
            }
            case 'number': {
                const numberFilter = filter as NumberFilter;
                return { title: (numberFilter.term as string) ?? 'Number', component: NumberFacetedFilter, filter: numberFilter, open: writable(false) };
            }
            case 'organization': {
                return { title: 'Organization', component: OrganizationFacetedFilter, filter, open: writable(false) };
            }
            case 'project': {
                return { title: 'Project', component: ProjectFacetedFilter, filter, open: writable(false) };
            }
            case 'reference': {
                return { title: 'Reference', component: ReferenceFacetedFilter, filter, open: writable(false) };
            }
            case 'session': {
                return { title: 'Session', component: SessionFacetedFilter, filter, open: writable(false) };
            }
            case 'status': {
                return { title: 'Status', component: StatusFacetedFilter, filter, open: writable(false) };
            }
            case 'string': {
                const stringFilter = filter as StringFilter;
                return { title: (stringFilter.term as string) ?? 'String', component: StringFacetedFilter, filter, open: writable(false) };
            }
            case 'type': {
                return { title: 'Type', component: TypeFacetedFilter, filter, open: writable(false) };
            }
            case 'version': {
                const versionFilter = filter as VersionFilter;
                return { title: (versionFilter.term as string) ?? 'Version', component: VersionFacetedFilter, filter, open: writable(false) };
            }
            default: {
                throw new Error(`Unknown filter type: ${filter.type}`);
            }
        }
    });
}
