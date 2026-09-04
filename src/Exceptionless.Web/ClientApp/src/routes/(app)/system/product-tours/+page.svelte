<script lang="ts">
    import type { ProductTourUsageRange } from '$features/admin/product-tour-usage';
    import type { ProductTourSummary } from '$generated/api';

    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import { getAdminProductTourUsageQuery } from '$features/admin/api.svelte';
    import ProductTourActivityInfo from '$features/admin/components/product-tour-activity-info.svelte';
    import ProductTourActivity from '$features/admin/components/product-tour-activity.svelte';
    import ProductTourPeriod from '$features/admin/components/product-tour-period.svelte';
    import { productTourCatalog } from '$features/product-tours/catalog';
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';

    let range = $state<ProductTourUsageRange>({
        days: 30,
        kind: 'days'
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

<div class="@container flex flex-col gap-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
        <Muted>Guide activity · {usage?.interval === 'month' ? 'Monthly · ' : usage?.interval === 'day' ? 'Daily · ' : ''}UTC</Muted>
        <div class="flex items-center gap-2" aria-label="Usage filters">
            <ProductTourPeriod bind:range />
            <Button variant="outline" size="icon" aria-label="Refresh tour activity" disabled={usageQuery.isFetching} onclick={() => usageQuery.refetch()}>
                <RefreshCw />
            </Button>
        </div>
    </div>

    {#if usage && !usage.collection_available}
        <Alert.Root>
            <Alert.Title>Guide activity collection is unavailable</Alert.Title>
            <Alert.Description
                >The internal storage project is unavailable. Guides and saved progress still work; the charts show previously recorded activity.</Alert.Description
            >
        </Alert.Root>
    {/if}

    {#if usageQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load guided-tour usage. Please try again.</p>
                <Button class="mt-3" variant="outline" onclick={() => usageQuery.refetch()}>Retry</Button>
            </Card.Content>
        </Card.Root>
    {:else if usageQuery.isPending}
        <div class="grid gap-4 @3xl:grid-cols-2">
            {#each [0, 1, 2, 3] as index (index)}
                <Skeleton class="h-56 rounded-xl" aria-label={`Loading guide activity ${index + 1}`} />
            {/each}
        </div>
    {:else if usage?.tours.length === 0}
        <p class="text-muted-foreground text-sm">No guided-tour activity was recorded in this period.</p>
    {:else}
        {@render TourCards(guides)}
        {#if invitations.length > 0}
            <section aria-labelledby="invitation-activity-title" class="flex flex-col gap-4">
                <h2 id="invitation-activity-title" class="text-sm font-medium">Invitations</h2>
                {@render TourCards(invitations)}
            </section>
        {/if}
    {/if}
</div>

{#snippet TourCards(tours: ProductTourSummary[])}
    <div class="grid gap-4 @3xl:grid-cols-2">
        {#each tours as tour (`${tour.name}:${tour.version}`)}
            <Card.Root aria-label={`${title(tour.name)} usage`}>
                <Card.Header class="flex flex-row flex-wrap items-center justify-between gap-x-3 gap-y-1">
                    <div class="flex items-center gap-1">
                        <Card.Title>{title(tour.name)} <span class="text-muted-foreground text-xs font-normal">v{tour.version}</span></Card.Title>
                        <ProductTourActivityInfo {tour} title={title(tour.name)} />
                    </div>
                    {#if tour.last_run_utc}
                        <Card.Description class="whitespace-nowrap">Last event <TimeAgo value={tour.last_run_utc} /></Card.Description>
                    {/if}
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
