<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as Typography from '$comp/typography';
    import * as Kbd from '$comp/ui/kbd';
    import { formatKeyboardShortcut } from '$features/shared/keyboard-shortcuts';

    import type { ProductTourShortcut } from '../models';

    let { description, shortcuts = [] }: { description: Snippet | string; shortcuts?: ProductTourShortcut[] } = $props();
</script>

<Typography.P class="leading-normal not-first:mt-0">
    {#if typeof description === 'string'}
        {description}
    {:else}
        {@render description()}
    {/if}
</Typography.P>
{#if shortcuts.length}
    <div class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-2">
        {#each shortcuts as { label, shortcut } (label)}
            <span class="inline-flex items-center gap-1.5 text-xs">
                {label}
                <Kbd.Root>{formatKeyboardShortcut(shortcut.keys)}</Kbd.Root>
            </span>
        {/each}
    </div>
{/if}
