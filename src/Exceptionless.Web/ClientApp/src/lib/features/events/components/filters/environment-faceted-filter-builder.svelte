<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import EnvironmentFacetedFilter from './environment-faceted-filter.svelte';
    import { EnvironmentFilter } from './models.svelte';

    const builder: FacetFilterBuilder<EnvironmentFilter> = {
        component: EnvironmentFacetedFilter,
        create: (filter) => filter ?? new EnvironmentFilter(),
        priority: 90,
        title: 'Environment'
    };
    builderContext.set('environment', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
