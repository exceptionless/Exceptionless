<script lang="ts">
    import type { ProductTourSummary } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';

    import { getRate } from '../product-tour-usage';

    let { tour }: { tour: ProductTourSummary } = $props();
    const invitation = $derived(tour.kind === 'prompt');
    const metrics = $derived([
        ...(invitation
            ? [
                  {
                      key: 'shown' as const,
                      label: 'Shown'
                  }
              ]
            : []),
        {
            key: 'started' as const,
            label: 'Started'
        },
        {
            key: 'completed' as const,
            label: invitation ? 'Accepted' : 'Completed'
        },
        {
            key: 'dismissed' as const,
            label: 'Dismissed'
        }
    ]);
</script>

<dl class={['grid gap-3', invitation ? 'grid-cols-2 sm:grid-cols-4' : 'grid-cols-3']} aria-label="Selected guide totals">
    {#each metrics as metric (metric.key)}
        {@const rate =
            metric.key === 'shown' || (!invitation && metric.key === 'started') ? null : getRate(tour[metric.key], invitation ? tour.shown : tour.started)}
        <div>
            <dt class="text-muted-foreground text-xs">{metric.label}</dt>
            <dd class="font-semibold tabular-nums"><Number value={tour[metric.key]} /></dd>
            {#if rate !== null}
                <dd class="text-muted-foreground text-xs"><Percentage percent={rate * 100} /> of {invitation ? 'invitations shown' : 'starts'}</dd>
            {/if}
        </div>
    {/each}
</dl>
