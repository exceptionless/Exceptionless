<script lang="ts">
    import * as Typography from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { ProductTourStatus } from '$features/users/models';
    import Bookmark from '@lucide/svelte/icons/bookmark';
    import Bot from '@lucide/svelte/icons/bot';
    import Folder from '@lucide/svelte/icons/folder';
    import Layers from '@lucide/svelte/icons/layers';
    import PanelLeft from '@lucide/svelte/icons/panel-left';

    import type { ProductTourListItem, ProductTourName } from '../../models';

    import ProductTourPrivacyLink from '../product-tour-privacy-link.svelte';

    interface Props {
        activeTourName?: ProductTourName;
        items: ProductTourListItem[];
        onStart: (name: ProductTourName) => Promise<void>;
        open?: boolean;
        ready: boolean;
        resumableTourName?: ProductTourName;
    }

    let { activeTourName, items, onStart, open = $bindable(false), ready, resumableTourName }: Props = $props();
    const id = $props.id();
    const icons = {
        'app-overview': PanelLeft,
        'event-investigate': Layers,
        'exie-overview': Bot,
        'project-configure': Folder,
        'saved-view-create': Bookmark
    };
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="max-h-[85vh] overflow-y-auto sm:max-w-2xl" data-product-tour-overlay>
        <Dialog.Header>
            <Dialog.Title>Guided Tours</Dialog.Title>
            <Dialog.Description>Choose a short, step-by-step guide. Guides use your workspace, not sample data.</Dialog.Description>
        </Dialog.Header>

        <ul aria-label="Available guides" class="divide-border divide-y">
            {#each items as item (item.name)}
                {@const Icon = icons[item.name]}
                {@const completed = item.progress?.status === ProductTourStatus.Completed && item.progress.version >= item.version}
                {@const actionLabel = resumableTourName === item.name ? 'Continue' : activeTourName === item.name || completed ? 'Restart' : 'Start'}
                <li>
                    <section aria-label={item.title} class="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3 gap-y-2 py-3 sm:grid-cols-[auto_minmax(0,1fr)_auto]">
                        <Icon aria-hidden="true" class="text-muted-foreground mt-0.5 size-5" />
                        <div class="min-w-0">
                            <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
                                <Typography.H3 class="text-sm">{item.title}</Typography.H3>
                                {#if completed}
                                    <Badge variant="secondary">Completed</Badge>
                                {/if}
                            </div>
                            <Typography.Muted class="mt-1">{item.description}</Typography.Muted>
                            {#if !item.currentAvailability.available}
                                <Typography.Muted class="mt-1 text-xs" id={`${id}-${item.name}-reason`}>{item.currentAvailability.reason}</Typography.Muted>
                            {/if}
                        </div>
                        <Button
                            aria-describedby={!item.currentAvailability.available ? `${id}-${item.name}-reason` : undefined}
                            aria-label={`${actionLabel} ${item.title}`}
                            class="col-start-2 min-w-20 justify-self-start sm:col-start-3 sm:row-start-1 sm:self-center pointer-coarse:min-h-11"
                            disabled={!ready || !item.currentAvailability.available}
                            onclick={() => onStart(item.name)}
                            size="sm"
                            variant="outline"
                        >
                            {actionLabel}
                        </Button>
                    </section>
                </li>
            {/each}
        </ul>
        <ProductTourPrivacyLink />
    </Dialog.Content>
</Dialog.Root>
