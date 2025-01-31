<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';

    import { NumberFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<NumberFilter> = $props();
</script>

<FacetedFilter.Number
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
></FacetedFilter.Number>
