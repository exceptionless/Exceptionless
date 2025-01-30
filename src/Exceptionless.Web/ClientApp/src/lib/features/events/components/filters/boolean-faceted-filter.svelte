<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';

    import type { BooleanFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<BooleanFilter> = $props();
</script>

<FacetedFilter.Boolean
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
></FacetedFilter.Boolean>
