<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Tooltip from '$comp/ui/tooltip';
    import Eraser from '@lucide/svelte/icons/eraser';
    import Eye from '@lucide/svelte/icons/eye';
    import EyeOff from '@lucide/svelte/icons/eye-off';
    import HelpCircle from '@lucide/svelte/icons/help-circle';
    import Trash2 from '@lucide/svelte/icons/trash-2';

    interface Props {
        clear: () => void;
        helpHref?: string;
        helpLabel?: string;
        hidden?: boolean;
        remove: () => void;
        showClear: boolean;
        toggleHidden?: () => void;
    }

    let { clear, helpHref, helpLabel = 'Open filter documentation', hidden = false, remove, showClear, toggleHidden }: Props = $props();
</script>

<div class="flex items-center justify-between gap-2 border-t px-2 py-1">
    {#if helpHref}
        <Tooltip.Root>
            <Tooltip.Trigger>
                {#snippet child({ props })}
                    <Button {...props} href={helpHref} target="_blank" rel="noopener noreferrer" variant="ghost" size="icon-sm" aria-label={helpLabel}>
                        <HelpCircle class="size-4 text-muted-foreground" />
                    </Button>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content>{helpLabel}</Tooltip.Content>
        </Tooltip.Root>
    {/if}
    <div class="ml-auto flex items-center gap-0.5">
        {#if showClear}
            <Tooltip.Root>
                <Tooltip.Trigger>
                    {#snippet child({ props })}
                        <Button {...props} variant="ghost" size="icon-sm" onclick={clear} aria-label="Clear filter value">
                            <Eraser class="size-4 text-muted-foreground" />
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
                                <Eye class="size-4 text-muted-foreground" />
                            {:else}
                                <EyeOff class="size-4 text-muted-foreground" />
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
                        <Trash2 class="size-4 text-muted-foreground" />
                    </Button>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content>Remove filter</Tooltip.Content>
        </Tooltip.Root>
    </div>
</div>
