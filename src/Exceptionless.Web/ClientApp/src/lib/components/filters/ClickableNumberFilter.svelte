<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { NumberFilter } from './filters';

    export let term: string;
    export let value: number | undefined;

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

<A on:click={onSearchClick} {title}>
    <slot />
</A>
