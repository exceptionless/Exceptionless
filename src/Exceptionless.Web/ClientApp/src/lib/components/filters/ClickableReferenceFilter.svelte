<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { ReferenceFilter } from './filters';

    export let value: string;

    const title = `Search reference:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new ReferenceFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title}>
    <slot />
</A>
