<script lang="ts">
    import { stackStatuses } from '$comp/events/options';
    import { StatusFilter } from '$comp/filters/filters.svelte';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';
    import { StackStatus } from '$lib/models/api';

    let { title = 'Status', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<StatusFilter> = $props();
</script>

<MultiselectFacetedFilter
    {title}
    values={filter.value}
    options={stackStatuses}
    changed={(values) => {
        filter.value = values as StackStatus[];
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {...props}
></MultiselectFacetedFilter>
