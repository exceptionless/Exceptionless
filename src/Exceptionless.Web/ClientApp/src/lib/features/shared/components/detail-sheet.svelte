<script lang="ts">
    import type { Snippet } from 'svelte';

    import { Button } from '$comp/ui/button';
    import * as Sheet from '$comp/ui/sheet';
    import ExternalLink from '@lucide/svelte/icons/external-link';

    import { preserveDetailSheetForAssistant } from './detail-sheet-interaction';

    interface Props {
        actions?: Snippet;
        children: Snippet;
        detailsHref: string;
        onClose: () => void;
        open: boolean;
        title: string;
    }

    let { actions, children, detailsHref, onClose, open, title }: Props = $props();

    function handleOpenChange(nextOpen: boolean) {
        if (!nextOpen) {
            onClose();
        }
    }
</script>

<Sheet.Root onOpenChange={handleOpenChange} {open}>
    <Sheet.Content
        class="bg-background top-15.25! bottom-0! z-40 h-auto! w-full scrollbar-gutter-stable gap-0 overflow-y-auto rounded-l-lg border-l text-base shadow-2xl duration-150 ease-out will-change-transform sm:max-w-full! md:w-5/6!"
        onInteractOutside={preserveDetailSheetForAssistant}
        overlayProps={{ class: 'top-15.25! z-40 bg-black/5 dark:bg-black/40 supports-backdrop-filter:backdrop-blur-[0.5px]' }}
        preventScroll={false}
    >
        <div class="absolute top-3 right-12 z-10 flex items-center gap-1">
            {@render actions?.()}
            <Button aria-label="Open details in new window" href={detailsHref} size="icon-sm" title="Open in new window" variant="ghost">
                <ExternalLink aria-hidden="true" />
            </Button>
        </div>
        <Sheet.Header class="sr-only">
            <Sheet.Title level={3}>{title}</Sheet.Title>
        </Sheet.Header>
        <div class="px-4 pt-4 pb-4">
            {@render children()}
        </div>
    </Sheet.Content>
</Sheet.Root>
