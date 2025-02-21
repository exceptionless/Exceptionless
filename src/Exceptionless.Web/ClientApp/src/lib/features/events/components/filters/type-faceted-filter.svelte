<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { eventTypes } from '$features/events/options';

    import { TypeFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Type', ...props }: FacetedFilterProps<TypeFilter> = $props();
</script>

<FacetedFilter.MultiSelect
    changed={(values: string[]) => {
        filter.value = values;
        filterChanged(filter);
    }}
    options={eventTypes}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {title}
    values={filter.value}
    {...props}
></FacetedFilter.MultiSelect>
