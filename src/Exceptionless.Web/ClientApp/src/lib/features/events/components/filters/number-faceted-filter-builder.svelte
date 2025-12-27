<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

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
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    $effect(() => {
        builderContext.set(`number-${term}`, builder as unknown as FacetFilterBuilder<IFilter>);
    });
</script>
