<script lang="ts">
    import { BooleanFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import BooleanFacetedFilter from './base/BooleanFacetedFilter.svelte';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<BooleanFilter> = $props();
</script>

<BooleanFacetedFilter
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    value={filter.value}
    {...props}
></BooleanFacetedFilter>
