<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { ProductTourStatus } from '$features/users/models';
    import Compass from '@lucide/svelte/icons/compass';

    import type { ProductTourListItem, ProductTourName } from '../../types';

    import { PRODUCT_TOUR_CHECKPOINTS } from '../../types';

    interface Props {
        activeTourName?: ProductTourName;
        items: ProductTourListItem[];
        onStart: (name: ProductTourName) => Promise<void>;
        open?: boolean;
        resumableTourName?: ProductTourName;
    }

    let { activeTourName, items, onStart, open = $bindable(false), resumableTourName }: Props = $props();
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="max-h-[85vh] overflow-y-auto sm:max-w-2xl" data-product-tour-overlay>
        <Dialog.Header>
            <Dialog.Title>Guided Tours</Dialog.Title>
            <Dialog.Description>Learn Exceptionless with short guides that use your real data.</Dialog.Description>
        </Dialog.Header>

        <div class="grid gap-3 py-2 sm:grid-cols-2">
            {#each items as item (item.name)}
                <section aria-label={item.title} class="border-border bg-card flex min-h-44 flex-col gap-3 rounded-lg border p-4">
                    <div class="flex items-start justify-between gap-3">
                        <div class="bg-primary/10 text-primary flex size-9 shrink-0 items-center justify-center rounded-lg">
                            <Compass aria-hidden="true" class="size-5" />
                        </div>
                        {#if item.progress?.status === ProductTourStatus.Completed && item.progress.version >= item.version}
                            <Badge variant="secondary">Completed</Badge>
                        {/if}
                    </div>
                    <div class="flex-1">
                        <h3 class="font-semibold">{item.title}</h3>
                        <p class="text-muted-foreground mt-1 text-sm">{item.description}</p>
                        <p class="text-muted-foreground mt-2 text-xs">
                            {item.name === 'app-overview' ? 'Up to ' : ''}{PRODUCT_TOUR_CHECKPOINTS[item.name].length} steps
                        </p>
                        {#if !item.currentAvailability.available}
                            <p class="text-muted-foreground mt-2 text-xs">{item.currentAvailability.reason}</p>
                        {/if}
                    </div>
                    <Button disabled={!item.currentAvailability.available} onclick={() => onStart(item.name)} variant="outline">
                        {resumableTourName === item.name
                            ? 'Continue'
                            : activeTourName === item.name || (item.progress?.status === ProductTourStatus.Completed && item.progress.version >= item.version)
                              ? 'Restart'
                              : 'Start'}
                    </Button>
                </section>
            {/each}
        </div>
    </Dialog.Content>
</Dialog.Root>
