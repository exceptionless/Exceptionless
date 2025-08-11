import type { FacetedFilterProps, IFilter } from './models';

import Actions from './faceted-filter-actions.svelte';
import BadgeLoading from './faceted-filter-badge-loading.svelte';
import BadgeValue from './faceted-filter-badge-value.svelte';
import BadgeValues from './faceted-filter-badge-values.svelte';
import Boolean from './faceted-filter-boolean.svelte';
import { builderContext, type FacetFilterBuilder } from './faceted-filter-builder-context.svelte';
import Root from './faceted-filter-builder.svelte';
import Dropdown from './faceted-filter-drop-down.svelte';
import Keyword from './faceted-filter-keyword.svelte';
import MultiSelect from './faceted-filter-multi-select.svelte';
import Number from './faceted-filter-number.svelte';
import String from './faceted-filter-string.svelte';
import { FacetedFilter } from './models';

export {
    Actions,
    BadgeLoading,
    BadgeValue,
    BadgeValues,
    Boolean,
    Dropdown,
    Actions as FacetedFilterActions,
    BadgeLoading as FacetedFilterBadgeLoading,
    BadgeValue as FacetedFilterBadgeValue,
    BadgeValues as FacetedFilterBadgeValues,
    Boolean as FacetedFilterBoolean,
    //
    Root as FacetedFilterBuilder,
    Dropdown as FacetedFilterDropdown,
    Keyword as FacetedFilterKeyword,
    MultiSelect as FacetedFilterMultiSelect,
    Number as FacetedFilterNumber,
    String as FacetedFilterString,
    Keyword,
    MultiSelect,
    Number,
    Root,
    String
};

export { builderContext, FacetedFilter };
export type { FacetedFilterProps, FacetFilterBuilder, IFilter };
