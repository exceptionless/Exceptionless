<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import { Kbd } from '$comp/ui/kbd';
    import * as Tooltip from '$comp/ui/tooltip';
    import { formatKeyboardShortcut } from '$shared/keyboard-shortcuts';
    import { toast } from 'svelte-sonner';

    interface Props {
        class?: string;
        maxVisible?: number;
        onTagClick?: (tag: string) => Promise<void> | void;
        tags?: null | string[];
    }

    let { class: className, maxVisible = Number.POSITIVE_INFINITY, onTagClick, tags }: Props = $props();

    const copyTagShortcut = $derived(formatKeyboardShortcut(['Alt']));
    const visibleTags = $derived(tags?.slice(0, maxVisible) ?? []);
    const hiddenTags = $derived(tags?.slice(maxVisible) ?? []);
    const tagList = $derived(tags?.join(', ') ?? '');

    async function handleTagClick(event: MouseEvent, tag: string): Promise<void> {
        event.preventDefault();
        event.stopPropagation();

        if (event.altKey || event.metaKey) {
            try {
                await navigator.clipboard.writeText(tag);
                toast.success(`Copied tag "${tag}" to clipboard.`);
            } catch {
                toast.error('Unable to copy tag to clipboard.');
            }

            return;
        }

        await onTagClick?.(tag);
    }
</script>

{#snippet tagBadge(tag: string)}
    <Badge
        variant="outline"
        class="border-border bg-muted text-muted-foreground group-hover/button:bg-accent group-hover/button:text-accent-foreground dark:border-muted-foreground/50 max-w-28 truncate rounded-md text-xs"
    >
        {tag}
    </Badge>
{/snippet}

{#snippet tag(tag: string)}
    {#if onTagClick}
        <Tooltip.Root>
            <Tooltip.Trigger>
                {#snippet child({ props })}
                    <Button
                        {...props}
                        type="button"
                        size="sm"
                        variant="ghost"
                        class="h-auto cursor-pointer p-0"
                        onclick={(event) => handleTagClick(event, tag)}
                    >
                        {@render tagBadge(tag)}
                    </Button>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content arrowClasses="hidden" class="border-border bg-popover text-popover-foreground border shadow-md" sideOffset={4}>
                Click to filter. <Kbd>{copyTagShortcut}</Kbd> click to copy.
            </Tooltip.Content>
        </Tooltip.Root>
    {:else}
        {@render tagBadge(tag)}
    {/if}
{/snippet}

<Tooltip.Provider>
    {#if visibleTags.length > 0}
        <div class={['flex flex-wrap items-center gap-1', className]} title={tagList} aria-label={`Tags: ${tagList}`}>
            {#each visibleTags as value (value)}
                {@render tag(value)}
            {/each}
            {#if hiddenTags.length > 0}
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props })}
                            <Badge
                                {...props}
                                variant="outline"
                                class="border-border bg-muted text-muted-foreground dark:border-muted-foreground/50 cursor-default rounded-md text-xs"
                            >
                                +{hiddenTags.length}
                            </Badge>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content
                        arrowClasses="hidden"
                        class="border-border bg-popover text-popover-foreground max-w-xs flex-col items-start gap-2 border px-3 py-2 shadow-md"
                        sideOffset={4}
                    >
                        <div class="flex flex-wrap gap-1">
                            {#each hiddenTags as value (value)}
                                {@render tag(value)}
                            {/each}
                        </div>
                    </Tooltip.Content>
                </Tooltip.Root>
            {/if}
        </div>
    {:else}
        <span class="text-muted-foreground">—</span>
    {/if}
</Tooltip.Provider>
