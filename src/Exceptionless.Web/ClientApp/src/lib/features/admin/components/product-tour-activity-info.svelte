<script lang="ts">
    import type { ProductTourSummary } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import { Button } from '$comp/ui/button';
    import * as Popover from '$comp/ui/popover';
    import Info from '@lucide/svelte/icons/info';

    let { title, tour }: { title: string; tour: ProductTourSummary } = $props();
    const headingId = $props.id();
    const commonExit = $derived(
        tour.steps?.filter((step) => step.dismissed > 0).toSorted((a, b) => b.dismissed - a.dismissed || a.step.localeCompare(b.step))[0]
    );
</script>

<Popover.Root>
    <Popover.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="ghost" size="icon-sm" aria-label={`${title} activity details`}>
                <Info aria-hidden="true" />
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" collisionPadding={16} class="max-h-80 max-w-[calc(100vw-2rem)] overflow-y-auto" role="dialog" aria-labelledby={headingId}>
        <h3 id={headingId} class="font-medium">{title}</h3>
        <p class="text-muted-foreground text-xs">Activity in the selected period, not unique people.</p>
        {#if commonExit}
            <p>Most common exit: {commonExit.step.replaceAll('-', ' ')} · <Number value={commonExit.dismissed} /></p>
        {/if}
        {#if tour.kind === 'guide'}
            {#if tour.steps?.length}
                <section class="flex flex-col gap-1">
                    <h4 class="font-medium">Steps reached · exits</h4>
                    <ul class="flex flex-col gap-1" aria-label="Guide steps reached and explicit exits">
                        {#each tour.steps as step (step.step)}
                            <li>{step.step.replaceAll('-', ' ')}: <Number value={step.reached} /> reached; <Number value={step.dismissed} /> closed here.</li>
                        {/each}
                    </ul>
                    <p class="text-muted-foreground text-xs">Exits count explicit closes, not tab closures or navigation.</p>
                </section>
            {/if}
            {#if tour.start_sources.length}
                <section class="flex flex-col gap-1">
                    <h4 class="font-medium">Opened from</h4>
                    <ul class="flex flex-col gap-1" aria-label="Guide entry points">
                        {#each tour.start_sources as source (source.source)}
                            <li>
                                {source.source.replaceAll('-', ' ')}: <Number value={source.count} />
                                {#if tour.started > 0}(<Percentage percent={(source.count / tour.started) * 100} /> of starts){/if}
                            </li>
                        {/each}
                    </ul>
                </section>
            {/if}
            {#if !tour.steps?.length && !tour.start_sources.length}
                <p>No step or entry-point activity recorded in this period.</p>
            {/if}
        {:else}
            <p>Shown counts invitation displays; Accepted counts invitations used to open a guide.</p>
        {/if}
    </Popover.Content>
</Popover.Root>
