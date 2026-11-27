<script lang="ts">
    import type { Component } from 'svelte';

    import AppWindow from '@lucide/svelte/icons/app-window';
    import Atom from '@lucide/svelte/icons/atom';
    import Braces from '@lucide/svelte/icons/braces';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Cog from '@lucide/svelte/icons/cog';
    import Globe from '@lucide/svelte/icons/globe';
    import Hexagon from '@lucide/svelte/icons/hexagon';
    import PanelsTopLeft from '@lucide/svelte/icons/panels-top-left';
    import Search from '@lucide/svelte/icons/search';
    import Server from '@lucide/svelte/icons/server';
    import Shield from '@lucide/svelte/icons/shield';
    import Smartphone from '@lucide/svelte/icons/smartphone';
    import SquareChevronRight from '@lucide/svelte/icons/square-chevron-right';
    import SquareTerminal from '@lucide/svelte/icons/square-terminal';
    import TabletSmartphone from '@lucide/svelte/icons/tablet-smartphone';
    import Triangle from '@lucide/svelte/icons/triangle';
    import { tick, untrack } from 'svelte';

    import { Button } from '#comp/ui/button/index.ts';
    import * as Collapsible from '#comp/ui/collapsible/index.ts';
    import * as InputGroup from '#comp/ui/input-group/index.ts';
    import { cn } from '#lib/utils.ts';

    import { findPlatform, LEGACY_PLATFORMS, type Platform, type PlatformIcon, POPULAR_PLATFORMS, searchPlatforms } from '../platforms';

    interface Props {
        onSelect: (platform: Platform) => void;
        selectedId?: string;
    }

    let { onSelect, selectedId }: Props = $props();

    const icons: Record<PlatformIcon, Component<{ 'aria-hidden'?: 'true' | boolean; class?: string }>> = {
        'app-window': AppWindow,
        atom: Atom,
        braces: Braces,
        cog: Cog,
        globe: Globe,
        hexagon: Hexagon,
        'panels-top-left': PanelsTopLeft,
        server: Server,
        shield: Shield,
        smartphone: Smartphone,
        'square-chevron-right': SquareChevronRight,
        'square-terminal': SquareTerminal,
        'tablet-smartphone': TabletSmartphone,
        triangle: Triangle
    };

    let query = $state('');
    let searchInput = $state<HTMLElement | null>(null);
    let changeButton = $state<HTMLElement | null>(null);
    const selected = $derived(findPlatform(selectedId));
    // A chosen platform (including one from ?type=) collapses to a single row so its instructions stay close.
    let expanded = $state(untrack(() => !findPlatform(selectedId)));

    $effect(() => {
        if (!selected) {
            expanded = true;
        }
    });

    async function change(): Promise<void> {
        expanded = true;
        await tick();
        searchInput?.focus();
    }

    function choose(platform: Platform): void {
        expanded = false;
        query = '';
        onSelect(platform);
    }

    async function keepSelection(): Promise<void> {
        expanded = false;
        query = '';
        await tick();
        changeButton?.focus();
    }
    const popular = $derived(searchPlatforms(query, POPULAR_PLATFORMS));
    const legacy = $derived(searchPlatforms(query, LEGACY_PLATFORMS));
    let legacyOpen = $state(false);
    // Searching or a selected .NET Framework entry opens the older group so matches are never hidden.
    const isLegacyOpen = $derived(legacyOpen || (query.trim().length > 0 && legacy.length > 0) || selected?.group === 'legacy');
</script>

{#snippet tile(platform: Platform)}
    {@const Icon = icons[platform.icon]}
    {@const isSelected = platform.id === selected?.id}
    <button
        aria-pressed={isSelected}
        class={cn(
            'group/tile bg-card flex min-w-0 items-center gap-2 rounded-lg border p-2 text-left transition-colors outline-none sm:gap-3 sm:p-3',
            'focus-visible:border-ring focus-visible:ring-ring/50 hover:border-foreground/25 hover:bg-muted/50 focus-visible:ring-3',
            isSelected && 'border-primary bg-primary/5 ring-primary/30 hover:border-primary hover:bg-primary/5 ring-2'
        )}
        data-platform={platform.id}
        onclick={() => choose(platform)}
        type="button"
    >
        <span
            class={cn(
                'bg-muted text-muted-foreground flex size-8 shrink-0 items-center justify-center rounded-md transition-colors sm:size-9',
                isSelected && 'bg-primary text-primary-foreground'
            )}
        >
            <Icon aria-hidden="true" class={cn('size-4.5', platform.icon === 'triangle' && 'rotate-180')} />
        </span>
        <span class="flex min-w-0 flex-col">
            <span class="text-foreground text-sm leading-tight font-medium break-words">{platform.label}</span>
            <span class="text-muted-foreground truncate text-xs">{platform.category}</span>
        </span>
    </button>
{/snippet}

<div class="flex flex-col gap-3" data-tour="project-configure-platform">
    {#if selected && !expanded}
        {@const Icon = icons[selected.icon]}
        <div class="bg-card ring-foreground/10 flex items-center gap-3 rounded-xl p-3 ring-1" data-selected-platform={selected.id}>
            <span class="bg-primary text-primary-foreground flex size-9 shrink-0 items-center justify-center rounded-md">
                <Icon aria-hidden="true" class={cn('size-4.5', selected.icon === 'triangle' && 'rotate-180')} />
            </span>
            <div class="flex min-w-0 flex-1 flex-col">
                <h2 class="text-muted-foreground text-xs font-medium" id="platform-picker-heading">Your platform</h2>
                <p class="text-foreground truncate text-sm font-semibold">
                    {selected.label} <span class="text-muted-foreground font-normal">· {selected.category}</span>
                </p>
            </div>
            <Button aria-label={`Change platform from ${selected.label}`} bind:ref={changeButton} onclick={change} size="sm" variant="outline">Change</Button>
        </div>
    {:else}
        <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <h2 class="text-foreground text-base font-semibold" id="platform-picker-heading">
                Pick your platform
                {#if selected}<span class="text-muted-foreground font-normal">· {selected.label}</span>{/if}
            </h2>
            <InputGroup.Root class="sm:max-w-64">
                <InputGroup.Addon>
                    <Search aria-hidden="true" />
                </InputGroup.Addon>
                <InputGroup.Input
                    aria-label="Search platforms"
                    bind:ref={searchInput}
                    bind:value={query}
                    placeholder="Search, e.g. Python or Vue"
                    type="search"
                />
            </InputGroup.Root>
        </div>

        <div aria-labelledby="platform-picker-heading" class="grid grid-cols-2 gap-2 lg:grid-cols-3 xl:grid-cols-4" role="group">
            {#each popular as platform (platform.id)}
                {@render tile(platform)}
            {/each}
        </div>

        {#if popular.length === 0 && legacy.length === 0}
            <p class="text-muted-foreground text-sm" role="status">
                No match for “{query.trim()}”. Pick
                <button class="text-primary underline-offset-4 hover:underline" onclick={() => choose(findPlatform('curl')!)} type="button"
                    >REST API (curl)</button
                >; it works from any language that can send HTTP.
            </p>
        {/if}

        {#if legacy.length > 0}
            <Collapsible.Root onOpenChange={(open) => (legacyOpen = open)} open={isLegacyOpen}>
                <Collapsible.Trigger
                    class="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 flex items-center gap-1 rounded-md text-sm outline-none focus-visible:ring-3"
                >
                    <ChevronDown aria-hidden="true" class={cn('size-4 transition-transform', isLegacyOpen && 'rotate-180')} />
                    Older .NET Framework
                </Collapsible.Trigger>
                <Collapsible.Content>
                    <div aria-label="Older .NET Framework platforms" class="mt-2 grid grid-cols-2 gap-2 lg:grid-cols-3 xl:grid-cols-4" role="group">
                        {#each legacy as platform (platform.id)}
                            {@render tile(platform)}
                        {/each}
                    </div>
                </Collapsible.Content>
            </Collapsible.Root>
        {/if}

        {#if selected}
            <div>
                <Button onclick={keepSelection} size="sm" variant="ghost">Keep {selected.label}</Button>
            </div>
        {/if}
    {/if}
</div>
