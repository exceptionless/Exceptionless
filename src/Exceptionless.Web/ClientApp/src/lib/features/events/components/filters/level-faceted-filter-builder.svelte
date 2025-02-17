<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import LevelFacetedFilter from './level-faceted-filter.svelte';
    import { LevelFilter } from './models.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Log Level' }: Props = $props();

    const builder: FacetFilterBuilder<LevelFilter> = {
        component: LevelFacetedFilter,
        create: (filter?: LevelFilter) => filter ?? new LevelFilter(),
        priority,
        title
    };

    builderContext.set('level', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
