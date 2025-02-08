<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import KeywordFacetedFilter from './keyword-faceted-filter.svelte';
    import { KeywordFilter } from './models.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Keyword' }: Props = $props();

    const builder: FacetFilterBuilder<KeywordFilter> = {
        component: KeywordFacetedFilter,
        create: (filter?: KeywordFilter) => filter ?? new KeywordFilter(),
        priority,
        title
    };

    builderContext.set('keyword', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
