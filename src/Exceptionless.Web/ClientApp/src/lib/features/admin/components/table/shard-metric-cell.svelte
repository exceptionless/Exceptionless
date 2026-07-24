<script lang="ts">
    import CircleCheck from '@lucide/svelte/icons/circle-check';
    import FileText from '@lucide/svelte/icons/file-text';
    import Server from '@lucide/svelte/icons/server';
    import TriangleAlert from '@lucide/svelte/icons/triangle-alert';

    interface Props {
        label: string;
        metricId: string;
        value: number;
    }

    let { label, metricId, value }: Props = $props();
</script>

<div class="flex items-center gap-2">
    {#if metricId === 'active_primary'}
        <FileText class="text-muted-foreground size-4" />
    {:else if metricId === 'active_total'}
        <CircleCheck class="size-4 text-green-600" />
    {:else if metricId === 'relocating'}
        <Server class="text-muted-foreground size-4" />
    {:else if metricId === 'unassigned'}
        {#if value > 0}
            <TriangleAlert class="size-4 text-amber-500" />
        {:else}
            <CircleCheck class="size-4 text-green-600" />
        {/if}
    {/if}
    {label}
</div>
