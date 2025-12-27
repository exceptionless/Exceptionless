<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import BooleanFacetedFilter from './boolean-faceted-filter.svelte';
    import { BooleanFilter } from './models.svelte';

    interface Props {
        priority?: number;
        term: string;
        title?: string;
    }

    const { priority = 0, term, title = 'Boolean' }: Props = $props();

    const builder: FacetFilterBuilder<BooleanFilter> = {
        component: BooleanFacetedFilter,
        create: (filter?: BooleanFilter) => filter ?? new BooleanFilter(term),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    $effect(() => {
        builderContext.set(`boolean-${term}`, builder as unknown as FacetFilterBuilder<IFilter>);
    });
</script>
