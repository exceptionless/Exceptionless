<script lang="ts">
    import { NumberFilter } from '$comp/filters/filters.svelte';
    import NumberFacetedFilter from './base/NumberFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<NumberFilter> = $props();
</script>

<NumberFacetedFilter
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
></NumberFacetedFilter>
