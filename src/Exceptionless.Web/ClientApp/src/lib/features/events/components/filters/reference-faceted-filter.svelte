<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';

    import { ReferenceFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Reference', ...props }: FacetedFilterProps<ReferenceFilter> = $props();
</script>

<FacetedFilter.String
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {title}
    value={filter.value}
    {...props}
></FacetedFilter.String>
