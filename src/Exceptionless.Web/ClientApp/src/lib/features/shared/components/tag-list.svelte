<script lang="ts">
    import { Muted } from '$comp/typography';
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

    const tagColorClasses = [
        'border-red-300 bg-red-100 text-red-900 dark:border-red-700 dark:bg-red-950/40 dark:text-red-200',
        'border-orange-300 bg-orange-100 text-orange-900 dark:border-orange-700 dark:bg-orange-950/40 dark:text-orange-200',
        'border-amber-300 bg-amber-100 text-amber-900 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200',
        'border-emerald-300 bg-emerald-100 text-emerald-900 dark:border-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-200',
        'border-sky-300 bg-sky-100 text-sky-900 dark:border-sky-700 dark:bg-sky-950/40 dark:text-sky-200',
        'border-indigo-300 bg-indigo-100 text-indigo-900 dark:border-indigo-700 dark:bg-indigo-950/40 dark:text-indigo-200',
        'border-violet-300 bg-violet-100 text-violet-900 dark:border-violet-700 dark:bg-violet-950/40 dark:text-violet-200',
        'border-fuchsia-300 bg-fuchsia-100 text-fuchsia-900 dark:border-fuchsia-700 dark:bg-fuchsia-950/40 dark:text-fuchsia-200'
    ];

    function hashTag(tag: string): number {
        let hash = 0;
        for (let index = 0; index < tag.length; index++) {
            hash = (hash << 5) - hash + tag.charCodeAt(index);
            hash |= 0;
        }

        return Math.abs(hash);
    }

    function getTagColorClass(tag: string): string {
        return tagColorClasses[hashTag(tag) % tagColorClasses.length]!;
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

{#snippet tagBadge(tag: string)}
    <Badge variant="outline" class={`max-w-28 truncate text-xs ${getTagColorClass(tag)}`}>{tag}</Badge>
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
            <Tooltip.Content>
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
                            <Badge {...props} variant="outline" class="cursor-default text-xs">+{hiddenTags.length}</Badge>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content class="max-w-xs">
                        <div class="flex flex-wrap gap-1">
                            {#each hiddenTags as value (value)}
                                {@render tag(value)}
                            {/each}
                        </div>
                        {#if onTagClick}
                            <Muted class="mt-1 text-xs">Click to filter. <Kbd>{copyTagShortcut}</Kbd> click to copy.</Muted>
                        {/if}
                    </Tooltip.Content>
                </Tooltip.Root>
            {/if}
        </div>
    {:else}
        <span class="text-muted-foreground">—</span>
    {/if}
</Tooltip.Provider>
