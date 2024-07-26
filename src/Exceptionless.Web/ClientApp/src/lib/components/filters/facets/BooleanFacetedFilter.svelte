<script lang="ts">
    import { BooleanFilter } from '$comp/filters/filters.svelte';
    import BooleanFacetedFilter from './base/BooleanFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<BooleanFilter> = $props();
</script>

<BooleanFacetedFilter
    value={filter.value}
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {...props}
></BooleanFacetedFilter>
