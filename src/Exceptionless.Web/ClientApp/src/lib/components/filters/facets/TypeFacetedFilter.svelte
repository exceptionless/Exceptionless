<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { TypeFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Type', ...props }: FacetedFilterProps<TypeFilter> = $props();
</script>

<MultiselectFacetedFilter
    changed={(values) => {
        filter.value = values;
        filterChanged(filter);
    }}
    options={eventTypes}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {title}
    values={filter.value}
    {...props}
></MultiselectFacetedFilter>
