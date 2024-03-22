import type { IFilter } from '$comp/filters/filters';
import type { ComponentType } from 'svelte';

import Root from './faceted-filter-builder.svelte';
import Dropdown from './faceted-filter-dropdown.svelte';
import Keyword from './faceted-filter-keyword.svelte';
import Multiselect from './faceted-filter-multiselect.svelte';

export type FacetedFilter = { title: string; component: ComponentType; filter: IFilter };

export {
    Root,
    Dropdown,
    Keyword,
    Multiselect,
    //
    Root as FacetedFilterBuilder,
    Dropdown as DropdownFacetedFilter,
    Keyword as KeywordFacetedFilter,
    Multiselect as MultiselectFacetedFilter
};
