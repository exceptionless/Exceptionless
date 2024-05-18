<script lang="ts">
    import { SessionFilter, type IFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: SessionFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, title = 'Session', filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<StringFacetedFilter {title} bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}
></StringFacetedFilter>
