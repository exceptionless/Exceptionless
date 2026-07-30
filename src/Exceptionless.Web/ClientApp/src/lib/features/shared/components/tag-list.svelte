<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import { Kbd } from '$comp/ui/kbd';
    import * as Tooltip from '$comp/ui/tooltip';
    import { toast } from 'svelte-sonner';
    import { SvelteSet } from 'svelte/reactivity';

    interface Props {
        class?: string;
        maxVisible?: number;
        onTagClick?: (tag: string) => Promise<void> | void;
        tags?: null | string[];
    }

    let { class: className, maxVisible = Number.POSITIVE_INFINITY, onTagClick, tags }: Props = $props();

    const visibleTags = $derived(tags?.slice(0, maxVisible) ?? []);
    const hiddenTags = $derived(tags?.slice(maxVisible) ?? []);
    const tagList = $derived(tags?.join(', ') ?? '');
    const truncatedTags = new SvelteSet<string>();

    function observeTruncation(node: HTMLElement, tag: string) {
        const badge = node.querySelector<HTMLElement>('[data-slot="badge"]');
        if (!badge) {
            return;
        }

        const badgeElement = badge;

        function updateTruncation() {
            const isTruncated = badgeElement.scrollWidth > badgeElement.clientWidth;
            if (truncatedTags.has(tag) === isTruncated) {
                return;
            }

            if (isTruncated) {
                truncatedTags.add(tag);
            } else {
                truncatedTags.delete(tag);
            }
        }

        updateTruncation();

        if (typeof ResizeObserver === 'undefined') {
            return;
        }

        const observer = new ResizeObserver(updateTruncation);
        observer.observe(badgeElement);

        return {
            destroy() {
                observer.disconnect();
            }
        };
    }

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

{#snippet tagBadge(tag: string, title?: string)}
    <Badge
        {title}
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
                        <span class="contents" use:observeTruncation={tag}>
                            {@render tagBadge(tag)}
                        </span>
                    </Button>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content
                arrowClasses="hidden"
                class="border-border bg-popover text-popover-foreground max-w-sm flex-col items-start border shadow-md"
                sideOffset={4}
            >
                {#if truncatedTags.has(tag)}
                    <span class="max-w-xs font-medium break-all">{tag}</span>
                {/if}
                <span class="flex items-center gap-1 whitespace-nowrap">
                    Click to filter. Hold
                    <Kbd
                        class="border-border in-data-[slot=tooltip-content]:bg-muted in-data-[slot=tooltip-content]:text-foreground dark:border-muted-foreground/50 dark:in-data-[slot=tooltip-content]:bg-muted border"
                    >
                        Alt / Option
                    </Kbd>
                    while clicking to copy.
                </span>
            </Tooltip.Content>
        </Tooltip.Root>
    {:else}
        <span class="contents" use:observeTruncation={tag}>
            {@render tagBadge(tag, truncatedTags.has(tag) ? tag : undefined)}
        </span>
    {/if}
{/snippet}

<Tooltip.Provider>
    {#if visibleTags.length > 0}
        <div class={['flex flex-wrap items-center gap-1', className]} aria-label={`Tags: ${tagList}`}>
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
