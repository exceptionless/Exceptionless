<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import { ReferenceFilter } from './models.svelte';
    import ReferenceFacetedFilter from './reference-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Reference' }: Props = $props();

    const builder: FacetFilterBuilder<ReferenceFilter> = {
        component: ReferenceFacetedFilter,
        create: (filter?: ReferenceFilter) => filter ?? new ReferenceFilter(),
        priority,
        title
    };

    builderContext.set('reference', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
