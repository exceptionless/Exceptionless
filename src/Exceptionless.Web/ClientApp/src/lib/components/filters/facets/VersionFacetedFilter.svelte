<script lang="ts">
    import { VersionFilter, type IFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: VersionFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<StringFacetedFilter bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}></StringFacetedFilter>
