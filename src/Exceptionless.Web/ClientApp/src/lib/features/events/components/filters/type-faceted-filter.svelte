<script lang="ts">
    import type { FacetedFilterProps } from '#comp/faceted-filter/index.ts';

    import * as FacetedFilter from '#comp/faceted-filter/index.ts';
    import { eventTypes } from '#features/events/options.ts';

    import { TypeFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Type', ...props }: FacetedFilterProps<TypeFilter> = $props();

    function toggleHidden() {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }
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
    hidden={filter.hidden}
    {title}
    {toggleHidden}
    values={filter.value}
    {...props}
></FacetedFilter.MultiSelect>
