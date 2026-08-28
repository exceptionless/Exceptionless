import type { PersistentEventKnownTypes } from '$features/events/models';
import type { LogLevel } from '$features/events/models/event-data';

import * as FacetedFilter from '$comp/faceted-filter';
import {
    BooleanFilter,
    LevelFilter,
    ProjectFilter,
    ReferenceFilter,
    SessionFilter,
    StatusFilter,
    StringFilter,
    TagFilter,
    TypeFilter,
    VersionFilter
} from '$features/events/components/filters';
import { StackStatus } from '$features/stacks/models';

import type { ListFilterQueryParams } from '../redirect-to-events.svelte';

export function getEventQueryFilters(params: ListFilterQueryParams): FacetedFilter.IFilter[] | null {
    const filters: FacetedFilter.IFilter[] = [];

    if (params.project) {
        filters.push(new ProjectFilter(splitQueryParam(params.project)));
    }
    if (params.stack) {
        filters.push(new StringFilter('stack', params.stack));
    }

    addBooleanFilter(filters, 'bot', params.bot);
    addBooleanFilter(filters, 'first', params.first);

    if (params.level) {
        filters.push(new LevelFilter(splitQueryParam(params.level) as LogLevel[]));
    }
    if (params.reference) {
        filters.push(new ReferenceFilter(params.reference));
    }
    if (params.session) {
        filters.push(new SessionFilter(params.session));
    }
    if (params.status) {
        filters.push(new StatusFilter(splitQueryParam(params.status) as StackStatus[]));
    }
    if (params.tag) {
        filters.push(new TagFilter(splitQueryParam(params.tag) as PersistentEventKnownTypes[]));
    }
    if (params.type) {
        filters.push(new TypeFilter(splitQueryParam(params.type) as PersistentEventKnownTypes[]));
    }
    if (params.version) {
        filters.push(new VersionFilter('version', params.version));
    }

    return filters.length > 0 ? filters : null;
}

function addBooleanFilter(filters: FacetedFilter.IFilter[], field: 'bot' | 'first', value: null | string | undefined): void {
    if (value === 'true' || value === 'false') {
        filters.push(new BooleanFilter(field, value === 'true'));
    }
}

function splitQueryParam(value: string): string[] {
    return value
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean);
}
