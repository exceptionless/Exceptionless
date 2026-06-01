<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Tooltip from '$comp/ui/tooltip';
    import Eraser from '@lucide/svelte/icons/eraser';
    import Eye from '@lucide/svelte/icons/eye';
    import EyeOff from '@lucide/svelte/icons/eye-off';
    import Trash2 from '@lucide/svelte/icons/trash-2';

    interface Props {
        clear: () => void;
        hidden?: boolean;
        remove: () => void;
        showClear: boolean;
        toggleHidden?: () => void;
    }

    let { clear, hidden = false, remove, showClear, toggleHidden }: Props = $props();
</script>

<div class="flex items-center justify-end gap-0.5 border-t px-2 py-1">
    {#if showClear}
        <Tooltip.Root>
            <Tooltip.Trigger>
                {#snippet child({ props })}
                    <Button {...props} variant="ghost" size="icon-sm" onclick={clear} aria-label="Clear filter value">
                        <Eraser class="text-muted-foreground size-4" />
                    </Button>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content>Clear value</Tooltip.Content>
        </Tooltip.Root>
    {/if}
    {#if toggleHidden}
        <Tooltip.Root>
            <Tooltip.Trigger>
                {#snippet child({ props })}
                    <Button {...props} variant="ghost" size="icon-sm" onclick={toggleHidden} aria-label={hidden ? 'Show filter' : 'Hide filter'}>
                        {#if hidden}
                            <Eye class="text-muted-foreground size-4" />
                        {:else}
                            <EyeOff class="text-muted-foreground size-4" />
                        {/if}
                    </Button>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content>{hidden ? 'Show filter' : 'Hide filter'}</Tooltip.Content>
        </Tooltip.Root>
    {/if}
    <Tooltip.Root>
        <Tooltip.Trigger>
            {#snippet child({ props })}
                <Button {...props} variant="ghost" size="icon-sm" onclick={remove} aria-label="Remove filter">
                    <Trash2 class="text-muted-foreground size-4" />
                </Button>
            {/snippet}
        </Tooltip.Trigger>
        <Tooltip.Content>Remove filter</Tooltip.Content>
    </Tooltip.Root>
</div>
