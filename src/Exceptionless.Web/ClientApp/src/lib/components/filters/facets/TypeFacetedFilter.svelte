<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { TypeFilter } from '$comp/filters/filters.svelte';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { title = 'Type', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<TypeFilter> = $props();
</script>

<MultiselectFacetedFilter
    {title}
    values={filter.value}
    options={eventTypes}
    changed={(values) => {
        filter.value = values;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {...props}
></MultiselectFacetedFilter>
