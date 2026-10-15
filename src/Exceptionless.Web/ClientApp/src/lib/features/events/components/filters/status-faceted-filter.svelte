<script lang="ts">
    import type { FacetedFilterProps } from '#comp/faceted-filter/index.ts';

    import * as FacetedFilter from '#comp/faceted-filter/index.ts';
    import { StackStatus } from '#features/stacks/models.ts';
    import { stackStatuses } from '#features/stacks/options.ts';

    import { StatusFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Status', ...props }: FacetedFilterProps<StatusFilter> = $props();

    function toggleHidden() {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }
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
    hidden={filter.hidden}
    {title}
    {toggleHidden}
    values={filter.value}
    {...props}
></FacetedFilter.MultiSelect>
