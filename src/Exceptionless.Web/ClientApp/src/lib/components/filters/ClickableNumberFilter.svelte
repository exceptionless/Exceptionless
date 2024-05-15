<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import A from '$comp/typography/A.svelte';
    import { NumberFilter } from './filters';

    let { term, value, ...props }: { term: string; value: number | undefined } = $props();
    const title = `Search ${term}:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new NumberFilter(term, value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} {...props}>
    <slot><IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" /></slot>
</A>
