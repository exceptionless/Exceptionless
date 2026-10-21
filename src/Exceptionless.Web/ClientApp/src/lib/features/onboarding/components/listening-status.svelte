<script lang="ts">
    import type { Snippet } from 'svelte';

    import ChevronDown from '@lucide/svelte/icons/chevron-down';

    import * as Card from '#comp/ui/card/index.ts';
    import * as Collapsible from '#comp/ui/collapsible/index.ts';
    import { cn } from '#lib/utils.ts';

    import type { Platform } from '../platforms';

    import { formatElapsed, getTroubleshootingTips, TROUBLESHOOTING_DELAY_SECONDS } from '../troubleshooting';

    interface Props {
        actions?: Snippet;
        platform?: Platform;
        serverUrl: string;
    }

    let { actions, platform, serverUrl }: Props = $props();

    let elapsedSeconds = $state(0);
    let troubleshootingOpen = $state(false);
    const showTroubleshooting = $derived(elapsedSeconds >= TROUBLESHOOTING_DELAY_SECONDS);
    const tips = $derived(getTroubleshootingTips(platform, serverUrl));

    $effect(() => {
        const startedAt = Date.now();
        const interval = setInterval(() => {
            elapsedSeconds = Math.floor((Date.now() - startedAt) / 1000);
        }, 1000);

        return () => clearInterval(interval);
    });

    $effect(() => {
        if (showTroubleshooting) {
            troubleshootingOpen = true;
        }
    });
</script>

<Card.Root class="border-primary/30" data-listening-status size="sm">
    <Card.Content class="flex flex-col gap-3">
        <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
            <span aria-hidden="true" class="relative flex size-3 shrink-0">
                <span class="bg-primary absolute inline-flex size-full rounded-full opacity-60 motion-safe:animate-ping"></span>
                <span class="bg-primary relative inline-flex size-3 rounded-full"></span>
            </span>
            <p class="text-foreground min-w-0 flex-1 text-sm font-medium" role="status">
                Listening for your first event…
                <span class="text-muted-foreground font-normal tabular-nums" aria-hidden="true">{formatElapsed(elapsedSeconds)}</span>
            </p>
            {#if actions}
                <div class="flex w-full flex-wrap gap-2 sm:w-auto">{@render actions()}</div>
            {/if}
        </div>

        {#if showTroubleshooting}
            <Collapsible.Root bind:open={troubleshootingOpen}>
                <Collapsible.Trigger
                    class="text-foreground hover:text-primary focus-visible:ring-ring/50 flex items-center gap-1 rounded-md text-sm font-medium outline-none focus-visible:ring-3"
                >
                    <ChevronDown aria-hidden="true" class={cn('size-4 transition-transform', troubleshootingOpen && 'rotate-180')} />
                    Not seeing anything?
                </Collapsible.Trigger>
                <Collapsible.Content>
                    <ul class="text-muted-foreground mt-2 flex list-disc flex-col gap-1.5 pl-9 text-sm">
                        {#each tips as tip (tip)}
                            <li>{tip}</li>
                        {/each}
                    </ul>
                </Collapsible.Content>
            </Collapsible.Root>
        {/if}
    </Card.Content>
</Card.Root>
