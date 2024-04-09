<script lang="ts">
    import IconSearch from '~icons/mdi/search';
    import A from '$comp/typography/A.svelte';
    import { cn } from '$lib/utils';
    import { DateFilter } from './filters';

    export let term: string;
    export let value: Date | string | undefined;

    let className: string | undefined | null = undefined;
    export { className as class };

    const title = `Search ${term}:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new DateFilter(term, value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} class={cn('ml-2', className)}>
    <slot><IconSearch /></slot>
</A>
