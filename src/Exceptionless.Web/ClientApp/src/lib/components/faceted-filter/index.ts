import type { IFilter } from '$comp/filters/filters';
import type { ComponentType } from 'svelte';

import Root from './faceted-filter-builder.svelte';
import Actions from './faceted-filter-actions.svelte';

import BadgeLoading from './faceted-filter-badge-loading.svelte';
import BadgeValue from './faceted-filter-badge-value.svelte';
import BadgeValues from './faceted-filter-badge-values.svelte';

export type FacetedFilter = { title: string; component: ComponentType; filter: IFilter };

export {
    Root,
    Actions,
    BadgeLoading,
    BadgeValue,
    BadgeValues,
    //
    Root as FacetedFilterBuilder,
    Actions as FacetedFilterActions,
    BadgeLoading as FacetedFilterBadgeLoading,
    BadgeValue as FacetedFilterBadgeValue,
    BadgeValues as FacetedFilterBadgeValues
};
