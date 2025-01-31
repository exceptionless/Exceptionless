<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { stackStatuses } from '$features/events/components/options';
    import { StackStatus } from '$features/stacks/models';

    import { StatusFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Status', ...props }: FacetedFilterProps<StatusFilter> = $props();
</script>

<FacetedFilter.MultiSelect
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
></FacetedFilter.MultiSelect>
