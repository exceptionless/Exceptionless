<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { StringFilter } from './filters';

    export let term: string;
    export let value: string | null | undefined;

    const title = `Search ${term}:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new StringFilter(term, value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title}>
    <slot />
</A>
