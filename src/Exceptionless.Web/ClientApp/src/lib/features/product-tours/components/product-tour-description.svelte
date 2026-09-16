<script lang="ts">
    import type { Snippet } from 'svelte';

    import { P } from '$comp/typography';
    let { description }: { description: Snippet | string } = $props();
</script>

<P class="leading-normal not-first:mt-0">
    {#if typeof description === 'string'}
        {description}
    {:else}
        {@render description()}
    {/if}
</P>
