<script lang="ts">
    import { StatusFilter } from '$comp/filters/filters.svelte';
    import { stackStatuses } from '$features/events/components/options';
    import { StackStatus } from '$features/stacks/models';

    import type { FacetedFilterProps } from '.';

    import MultiselectFacetedFilter from './base/multiselect-faceted-filter.svelte';

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
