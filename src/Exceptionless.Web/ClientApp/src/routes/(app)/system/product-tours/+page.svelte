<script lang="ts">
    import type { ProductTourUsageRange } from '$features/admin/product-tour-usage';
    import type { ProductTourSummary } from '$generated/api';

    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import { getAdminProductTourUsageQuery } from '$features/admin/api.svelte';
    import ProductTourActivity from '$features/admin/components/product-tour-activity.svelte';
    import ProductTourPeriod from '$features/admin/components/product-tour-period.svelte';
    import { productTourCatalog } from '$features/product-tours/catalog';
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';

    let range = $state<ProductTourUsageRange>({
        kind: 'history'
    });
    const usageQuery = getAdminProductTourUsageQuery(() => range);
    const usage = $derived(usageQuery.data);
    const guides = $derived(usage?.tours.filter((tour) => tour.kind === 'guide') ?? []);
    const invitations = $derived(usage?.tours.filter((tour) => tour.kind === 'prompt') ?? []);

    function title(value: string): string {
        const guide = productTourCatalog.find((tour) => tour.name === value);
        if (guide) {
            return guide.title;
        }

        if (value === 'app-welcome') {
            return 'Welcome invitation';
        }

        if (value === 'exie-announcement') {
            return 'Exie invitation';
        }
        return value
            .split('-')
            .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
            .join(' ');
    }
</script>

<div class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
        <Muted>Recorded guide activity across your installation</Muted>
        <div class="flex items-center gap-2" aria-label="Usage filters">
            <ProductTourPeriod bind:range />
            <Button variant="outline" size="icon" aria-label="Refresh tour activity" disabled={usageQuery.isFetching} onclick={() => usageQuery.refetch()}>
                <RefreshCw class="size-4" />
            </Button>
        </div>
    </div>
    <p class="text-muted-foreground text-sm">
        Charts show recorded events. Saved completion badges can include earlier, unrecorded activity.
        {range.kind === 'history' ? 'History is grouped by month.' : 'This month is grouped by day.'} Dates use UTC.
    </p>

    {#if usageQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load guided-tour usage. Please try again.</p>
                <Button class="mt-3" variant="outline" onclick={() => usageQuery.refetch()}>Retry</Button>
            </Card.Content>
        </Card.Root>
    {:else if usageQuery.isPending}
        <div class="grid gap-4 lg:grid-cols-2">
            {#each [0, 1, 2, 3] as index (index)}
                <Skeleton class="h-56 rounded-xl" aria-label={`Loading guide activity ${index + 1}`} />
            {/each}
        </div>
    {:else if usage?.tours.length === 0}
        <p class="text-muted-foreground text-sm">No guided-tour activity was recorded in this period.</p>
    {:else}
        {@render TourCards(guides)}
        {#if invitations.length > 0}
            <details>
                <summary class="cursor-pointer text-sm font-medium">Invitation activity</summary>
                <p class="text-muted-foreground my-4 text-sm">
                    Welcome and feature invitations offer a guide. Shown counts the invitation appearing—not the guide running. Accepted means starting a guide
                    or browsing guides.
                </p>
                {@render TourCards(invitations)}
            </details>
        {/if}
    {/if}
</div>

{#snippet TourCards(tours: ProductTourSummary[])}
    <div class="grid items-start gap-4 lg:grid-cols-2">
        {#each tours as tour (`${tour.name}:${tour.version}`)}
            <Card.Root aria-label={`${title(tour.name)} usage`}>
                <Card.Header>
                    <Card.Title>{title(tour.name)} <span class="text-muted-foreground text-xs font-normal">v{tour.version}</span></Card.Title>
                </Card.Header>
                <Card.Content>
                    {#if usage}
                        <ProductTourActivity {tour} interval={usage.interval} start={usage.utc_start} end={usage.utc_end} />
                    {/if}
                </Card.Content>
            </Card.Root>
        {/each}
    </div>
{/snippet}
