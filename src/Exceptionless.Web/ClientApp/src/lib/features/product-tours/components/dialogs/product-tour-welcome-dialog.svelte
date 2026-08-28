<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import Compass from '@lucide/svelte/icons/compass';

    import type { ProductTourListItem } from '../../types';

    interface Props {
        onBrowse: () => Promise<void>;
        onDismiss: () => Promise<void>;
        onStart: () => Promise<void>;
        open?: boolean;
        recommended: ProductTourListItem;
    }

    let { onBrowse, onDismiss, onStart, open = $bindable(false), recommended }: Props = $props();

    async function onOpenChange(nextOpen: boolean): Promise<void> {
        if (!nextOpen && open) {
            await onDismiss();
        }
    }
</script>

<Dialog.Root {open} {onOpenChange}>
    <Dialog.Content class="sm:max-w-lg" data-product-tour-overlay>
        <Dialog.Header>
            <div class="bg-primary/10 text-primary mb-2 flex size-11 items-center justify-center rounded-xl">
                <Compass aria-hidden="true" />
            </div>
            <Dialog.Title>Welcome to Exceptionless</Dialog.Title>
            <Dialog.Description>Take a short guided tour now, or browse the guides whenever you need them.</Dialog.Description>
        </Dialog.Header>

        <section class="border-border bg-muted/30 rounded-lg border p-4">
            <p class="text-sm font-semibold">Recommended: {recommended.title}</p>
            <p class="text-muted-foreground mt-1 text-sm">{recommended.description}</p>
        </section>

        <Dialog.Footer class="gap-2 sm:justify-between">
            <Button onclick={onDismiss} variant="ghost">Skip</Button>
            <div class="flex flex-col-reverse gap-2 sm:flex-row">
                <Button onclick={onBrowse} variant="outline">Browse Guides</Button>
                <Button onclick={onStart}>{recommended.name === 'configure-project' ? 'Continue setup' : 'Explore Exceptionless'}</Button>
            </div>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
