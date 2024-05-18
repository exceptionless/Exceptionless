<script lang="ts">
    import { KeywordFilter, type IFilter } from '$comp/filters/filters';
    import KeywordFacetedFilter from './base/KeywordFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: KeywordFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, title = 'Keyword', filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<KeywordFacetedFilter {title} bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}
></KeywordFacetedFilter>
