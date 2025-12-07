<script lang="ts">
    import Badge from '$comp/ui/badge/badge.svelte';
    import * as Tooltip from '$comp/ui/tooltip';

    interface Props {
        isOverLimit?: boolean;
    }

    let { isOverLimit = false }: Props = $props();
</script>

{#if isOverLimit}
    <Tooltip.Root>
        <Tooltip.Trigger>
            {#snippet child({ props })}
                <Badge {...props} variant="orange" class="cursor-help">Over Limit</Badge>
            {/snippet}
        </Tooltip.Trigger>
        <Tooltip.Content>This organization has exceeded its monthly event limit.</Tooltip.Content>
    </Tooltip.Root>
{/if}
