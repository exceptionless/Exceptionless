<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import type { StackStatus } from '$lib/models/api.generated';
    import { StatusFilter } from './filters';

    export let value: StackStatus[];

    const title = `Search status:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new StatusFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title}>
    <slot />
</A>
