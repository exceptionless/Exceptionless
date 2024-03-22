<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { TypeFilter } from './filters';

    export let value: string[];

    const title = `Search type:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new TypeFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title}>
    <slot />
</A>
