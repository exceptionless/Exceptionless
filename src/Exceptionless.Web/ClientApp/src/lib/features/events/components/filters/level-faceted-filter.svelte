<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { logLevels } from '$features/events/options';

    import { LevelFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Log Level', ...props }: FacetedFilterProps<LevelFilter> = $props();
</script>

<FacetedFilter.MultiSelect
    changed={(values: string[]) => {
        filter.value = values;
        filterChanged(filter);
    }}
    options={logLevels}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {title}
    values={filter.value}
    {...props}
></FacetedFilter.MultiSelect>
