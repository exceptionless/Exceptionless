<script lang="ts">
    import type { ProductTourSummary } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import * as Typography from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Popover from '$comp/ui/popover';
    import Info from '@lucide/svelte/icons/info';

    let { title, tour }: { title: string; tour: ProductTourSummary } = $props();
    const headingId = $props.id();
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
        <Typography.H3 id={headingId} class="text-sm font-medium">{title}</Typography.H3>
        <Typography.Muted class="text-xs">Activity in the selected period, not unique people.</Typography.Muted>
        {#if tour.kind === 'guide'}
            {#if tour.start_sources.length}
                <section class="flex flex-col gap-1">
                    <Typography.H4 class="text-sm font-medium">Opened from</Typography.H4>
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
            {#if !tour.start_sources.length}
                <Typography.P class="leading-normal not-first:mt-0">No entry-point activity recorded in this period.</Typography.P>
            {/if}
        {:else}
            <Typography.P class="leading-normal not-first:mt-0"
                >Shown counts invitation displays; Accepted counts invitations used to open a guide.</Typography.P
            >
        {/if}
    </Popover.Content>
</Popover.Root>
