<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onMount } from 'svelte';

    import DateFacetedFilter from './date-faceted-filter.svelte';
    import { DateFilter } from './models.svelte';

    interface Props {
        priority?: number;
        term: string;
        title?: string;
    }

    const { priority = 0, term, title = 'Date Range' }: Props = $props();

    onMount(() => {
        const builder: FacetFilterBuilder<DateFilter> = {
            component: DateFacetedFilter,
            create: (filter?: DateFilter) => filter ?? new DateFilter(term),
            priority,
            title
        };

        builderContext.set(`date-${term}`, builder as unknown as FacetFilterBuilder<IFilter>);
        return () => builderContext.delete('date');
    });
</script>
