<script lang="ts">
    import { stackStatuses } from '$comp/events/options';
    import { StatusFilter } from '$comp/filters/filters.svelte';
    import { StackStatus } from '$lib/models/api';

    import type { FacetedFilterProps } from '.';

    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Status', ...props }: FacetedFilterProps<StatusFilter> = $props();
</script>

<MultiselectFacetedFilter
    changed={(values) => {
        filter.value = values as StackStatus[];
        filterChanged(filter);
    }}
    options={stackStatuses}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {title}
    values={filter.value}
    {...props}
></MultiselectFacetedFilter>
