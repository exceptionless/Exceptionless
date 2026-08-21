<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import { StringFilter } from './models.svelte';
    import StringFacetedFilter from './string-faceted-filter.svelte';

    interface Props {
        priority?: number;
        term: string;
        title?: string;
    }

    const { priority = 0, term, title = 'String' }: Props = $props();

    const builder: FacetFilterBuilder<StringFilter> = {
        component: StringFacetedFilter,
        create: (filter?: StringFilter) => filter ?? new StringFilter(term),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    $effect(() => {
        const key = `string-${term}`;
        const registeredBuilder = builder as unknown as FacetFilterBuilder<IFilter>;
        builderContext.set(key, registeredBuilder);
        return () => {
            if (builderContext.get(key) === registeredBuilder) {
                builderContext.delete(key);
            }
        };
    });
</script>
