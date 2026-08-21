<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import Compass from '@lucide/svelte/icons/compass';

    import type { ProductTourListItem } from '../types';

    interface Props {
        onBrowse: () => void;
        onSkip: () => void;
        onStart: () => void;
        open?: boolean;
        recommended: ProductTourListItem;
    }

    let { onBrowse, onSkip, onStart, open = $bindable(false), recommended }: Props = $props();
</script>

<Dialog.Root bind:open>
    <Dialog.Content
        class="sm:max-w-lg"
        data-product-tour-overlay
        onEscapeKeydown={(event) => event.preventDefault()}
        onInteractOutside={(event) => event.preventDefault()}
        showCloseButton={false}
    >
        <Dialog.Header>
            <div class="bg-primary/10 text-primary mb-2 flex size-11 items-center justify-center rounded-xl">
                <Compass aria-hidden="true" />
            </div>
            <Dialog.Title>Welcome to the new Exceptionless UI</Dialog.Title>
            <Dialog.Description>Take a short guided tour now, or browse the guides whenever you need them.</Dialog.Description>
        </Dialog.Header>

        <section class="border-border bg-muted/30 rounded-lg border p-4">
            <p class="text-sm font-semibold">Recommended: {recommended.title}</p>
            <p class="text-muted-foreground mt-1 text-sm">{recommended.description}</p>
        </section>

        <Dialog.Footer class="gap-2 sm:justify-between">
            <Button onclick={onSkip} variant="ghost">Skip</Button>
            <div class="flex flex-col-reverse gap-2 sm:flex-row">
                <Button onclick={onBrowse} variant="outline">Browse Guides</Button>
                <Button onclick={onStart}>{recommended.id === 'configure-project' ? 'Continue setup' : 'Explore the new UI'}</Button>
            </div>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
