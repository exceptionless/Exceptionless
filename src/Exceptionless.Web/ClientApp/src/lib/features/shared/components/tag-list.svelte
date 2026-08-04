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

    function observeTruncation(tagText: HTMLElement, tag: string) {
        function updateTruncation() {
            const isTruncated = tagText.scrollWidth > tagText.clientWidth;
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
        observer.observe(tagText);

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

{#snippet tagBadge(tag: string, showFullValue = false)}
    <Badge
        variant="outline"
        class={[
            'border-border bg-muted text-muted-foreground group-hover/button:bg-accent group-hover/button:text-accent-foreground dark:border-muted-foreground/50 rounded-md text-xs',
            showFullValue ? 'h-auto max-w-full py-0.5 whitespace-normal' : 'max-w-28'
        ]}
    >
        <span class={showFullValue ? 'max-w-full break-all whitespace-normal' : 'min-w-0 truncate'} use:observeTruncation={tag}>
            {tag}
        </span>
    </Badge>
{/snippet}

{#snippet tagActionHint()}
    <span class="flex flex-wrap items-center gap-1">
        Click to filter. Hold
        <Kbd
            class="border-border in-data-[slot=tooltip-content]:bg-muted in-data-[slot=tooltip-content]:text-foreground dark:border-muted-foreground/50 dark:in-data-[slot=tooltip-content]:bg-muted border"
        >
            Alt / Option
        </Kbd>
        while clicking to copy.
    </span>
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
            <Tooltip.Content
                arrowClasses="hidden"
                class="border-border bg-popover text-popover-foreground max-w-[calc(100vw-2rem)] flex-col items-start border shadow-md sm:max-w-sm"
                sideOffset={4}
            >
                {#if truncatedTags.has(tag)}
                    <span class="max-w-xs font-medium break-all">{tag}</span>
                {/if}
                {@render tagActionHint()}
            </Tooltip.Content>
        </Tooltip.Root>
    {:else}
        <Tooltip.Root disabled={!truncatedTags.has(tag)}>
            <Tooltip.Trigger>
                {#snippet child({ props })}
                    <span {...props} class="inline-flex min-w-0">
                        {@render tagBadge(tag)}
                    </span>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content
                arrowClasses="hidden"
                class="border-border bg-popover text-popover-foreground max-w-[calc(100vw-2rem)] border shadow-md sm:max-w-sm"
                side="bottom"
                sideOffset={4}
            >
                <span class="max-w-xs font-medium break-all">{tag}</span>
            </Tooltip.Content>
        </Tooltip.Root>
    {/if}
{/snippet}

{#snippet overflowTag(tag: string)}
    {#if onTagClick}
        <Button type="button" size="sm" variant="ghost" class="h-auto max-w-full cursor-pointer p-0" onclick={(event) => handleTagClick(event, tag)}>
            {@render tagBadge(tag, true)}
        </Button>
    {:else}
        {@render tagBadge(tag, true)}
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
                        class="border-border bg-popover text-popover-foreground max-w-[calc(100vw-2rem)] flex-col items-start gap-2 border px-3 py-2 shadow-md sm:max-w-sm"
                        sideOffset={4}
                    >
                        <div class="flex flex-wrap gap-1">
                            {#each hiddenTags as value (value)}
                                {@render overflowTag(value)}
                            {/each}
                        </div>
                        {#if onTagClick}
                            {@render tagActionHint()}
                        {/if}
                    </Tooltip.Content>
                </Tooltip.Root>
            {/if}
        </div>
    {:else}
        <span class="text-muted-foreground">—</span>
    {/if}
</Tooltip.Provider>
