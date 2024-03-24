import type { FacetedFilter } from '$comp/faceted-filter';
import type { BooleanFilter, DateFilter, IFilter, NumberFilter, StringFilter, VersionFilter } from '../filters';

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
                return { title: booleanFilter.term as string, component: BooleanFacetedFilter, filter };
            }
            case 'date': {
                const dateFilter = filter as DateFilter;
                const title = dateFilter.term === 'date' ? 'Date Range' : dateFilter.term;
                return { title, component: DateFacetedFilter, filter };
            }
            case 'keyword': {
                return { title: 'Keyword', component: KeywordFacetedFilter, filter };
            }
            case 'number': {
                const numberFilter = filter as NumberFilter;
                return { title: numberFilter.term as string, component: NumberFacetedFilter, filter: numberFilter };
            }
            case 'organization': {
                return { title: 'Organization', component: OrganizationFacetedFilter, filter };
            }
            case 'project': {
                return { title: 'Project', component: ProjectFacetedFilter, filter };
            }
            case 'reference': {
                return { title: 'Reference', component: ReferenceFacetedFilter, filter };
            }
            case 'session': {
                return { title: 'Session', component: SessionFacetedFilter, filter };
            }
            case 'status': {
                return { title: 'Status', component: StatusFacetedFilter, filter };
            }
            case 'string': {
                const stringFilter = filter as StringFilter;
                return { title: stringFilter.term as string, component: StringFacetedFilter, filter };
            }
            case 'type': {
                return { title: 'Type' as string, component: TypeFacetedFilter, filter };
            }
            case 'version': {
                const versionFilter = filter as VersionFilter;
                return { title: versionFilter.term as string, component: VersionFacetedFilter, filter };
            }
            default: {
                throw new Error(`Unknown filter type: ${filter.type}`);
            }
        }
    });
}
