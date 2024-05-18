<script lang="ts">
    import { ReferenceFilter, type IFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: ReferenceFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { title = 'Reference', filter, filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<StringFacetedFilter {title} bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}
></StringFacetedFilter>
