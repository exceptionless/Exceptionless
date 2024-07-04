<script lang="ts">
    import { stackStatuses } from '$comp/events/options';
    import { StatusFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { title = 'Status', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<StatusFilter> = $props();
</script>

<MultiselectFacetedFilter
    {title}
    bind:values={filter.value}
    options={stackStatuses}
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></MultiselectFacetedFilter>
