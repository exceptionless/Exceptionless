<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { TypeFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { title = 'Type', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<TypeFilter> = $props();
</script>

<MultiselectFacetedFilter
    {title}
    bind:values={filter.value}
    options={eventTypes}
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></MultiselectFacetedFilter>
