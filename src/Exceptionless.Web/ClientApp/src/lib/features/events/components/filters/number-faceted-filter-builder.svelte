<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onDestroy } from 'svelte';

    import { NumberFilter } from './models.svelte';
    import NumberFacetedFilter from './number-faceted-filter.svelte';

    interface Props {
        priority?: number;
        term: string;
        title?: string;
    }

    const { priority = 0, term, title = 'Number' }: Props = $props();

    const builder: FacetFilterBuilder<NumberFilter> = {
        component: NumberFacetedFilter,
        create: (filter?: NumberFilter) => filter ?? new NumberFilter(term),
        priority,
        title
    };

    builderContext.set(`number-${term}`, builder as unknown as FacetFilterBuilder<IFilter>);
    onDestroy(() => builderContext.delete('number'));
</script>
