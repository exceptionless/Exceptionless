<script lang="ts">
    import Number from '$comp/formatters/number.svelte';

    interface Props {
        failedShards: number;
        successfulShards: number;
        totalShards: number;
    }

    let { failedShards, successfulShards, totalShards }: Props = $props();
</script>

<span class={failedShards > 0 ? 'text-destructive font-semibold' : 'text-muted-foreground'}>
    <Number value={successfulShards} />/<Number value={totalShards} />
    {#if failedShards > 0}
        <span class="text-destructive ml-1">(<Number value={failedShards} /> failed)</span>
    {/if}
</span>
