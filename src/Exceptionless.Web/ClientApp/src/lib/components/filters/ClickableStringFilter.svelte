<script lang="ts">
    import IconSearch from '~icons/mdi/search';
    import A from '$comp/typography/A.svelte';
    import { cn } from '$lib/utils';
    import { StringFilter } from './filters';

    export let term: string;
    export let value: string | null | undefined;

    const title = `Search ${term}:${value}`;

    let className: string | undefined | null = undefined;
    export { className as class };

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new StringFilter(term, value ?? undefined)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} class={cn('ml-2', className)}>
    <slot><IconSearch /></slot>
</A>
