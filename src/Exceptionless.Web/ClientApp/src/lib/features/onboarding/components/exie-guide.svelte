<script lang="ts">
    import type { Snippet } from 'svelte';

    import { cn } from '#lib/utils.ts';

    import type { ExieMood } from '../models';

    import ExieMascot from './exie-mascot.svelte';

    interface Props {
        actions?: Snippet;
        /** Speech bubble body. Exie speaks in the first person. */
        children: Snippet;
        class?: string;
        /** `stacked` puts the bubble above Exie; `inline` puts it beside Exie. */
        layout?: 'inline' | 'stacked';
        /** Announce bubble changes to assistive technology. */
        live?: boolean;
        mood?: ExieMood;
        size?: number;
        title?: string;
    }

    let { actions, children, class: className, layout = 'inline', live = true, mood = 'idle', size = 64, title }: Props = $props();
</script>

<div class={cn('flex gap-3', layout === 'stacked' ? 'flex-col-reverse items-start' : 'items-end', className)} data-exie-guide data-mood={mood}>
    <ExieMascot {mood} {size} />

    <div
        class={cn(
            'exie-bubble bg-card text-card-foreground ring-foreground/10 relative min-w-0 flex-1 rounded-xl px-4 py-3 text-sm shadow-sm ring-1',
            layout === 'stacked' ? 'exie-bubble-stacked' : 'exie-bubble-inline'
        )}
    >
        <div aria-atomic="true" aria-live={live ? 'polite' : undefined} class="flex flex-col gap-1">
            {#if title}
                <p class="text-foreground font-semibold">{title}</p>
            {/if}
            <div class="text-muted-foreground leading-relaxed">
                {@render children()}
            </div>
        </div>

        {#if actions}
            <div class="mt-3 flex flex-wrap items-center gap-2">
                {@render actions()}
            </div>
        {/if}
    </div>
</div>

<style>
    /* The tail points at Exie; it uses the same surface and ring as the bubble. */
    .exie-bubble::before {
        background: var(--card);
        box-shadow: -1px 1px 0 0 color-mix(in oklab, var(--foreground) 10%, transparent);
        content: '';
        height: 0.75rem;
        position: absolute;
        width: 0.75rem;
    }

    .exie-bubble-inline::before {
        bottom: 1.25rem;
        left: -0.375rem;
        transform: rotate(45deg);
    }

    .exie-bubble-stacked::before {
        bottom: -0.375rem;
        left: 1.75rem;
        transform: rotate(-45deg);
    }

    @media (prefers-reduced-motion: no-preference) {
        .exie-bubble {
            animation: exie-bubble-in 220ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
        }
    }

    @keyframes exie-bubble-in {
        from {
            opacity: 0;
            transform: translateY(4px) scale(0.98);
        }

        to {
            opacity: 1;
            transform: none;
        }
    }
</style>
