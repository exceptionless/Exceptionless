<script lang="ts">
    import OverLimitIndicator from '../over-limit-indicator.svelte';

    interface Props {
        isOverLimit?: boolean;
    }

    let { isOverLimit = false }: Props = $props();
</script>

{#if isOverLimit}
    <OverLimitIndicator {isOverLimit} />
{:else}
    -
{/if}
