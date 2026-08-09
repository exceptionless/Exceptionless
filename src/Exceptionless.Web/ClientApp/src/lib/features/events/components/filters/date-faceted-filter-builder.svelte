<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import DateFacetedFilter from './date-faceted-filter.svelte';
    import { DateFilter } from './models.svelte';

    interface Props {
        priority?: number;
        term: string;
        title?: string;
    }

    const { priority = 0, term, title = 'Date Range' }: Props = $props();

    const builder: FacetFilterBuilder<DateFilter> = {
        component: DateFacetedFilter,
        create: (filter?: DateFilter) => filter ?? new DateFilter(term),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    $effect(() => {
        const key = `date-${term}`;
        const registeredBuilder = builder as unknown as FacetFilterBuilder<IFilter>;
        builderContext.set(key, registeredBuilder);
        return () => {
            if (builderContext.get(key) === registeredBuilder) {
                builderContext.delete(key);
            }
        };
    });
</script>
