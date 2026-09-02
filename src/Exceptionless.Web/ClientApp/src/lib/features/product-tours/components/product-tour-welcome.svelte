<script lang="ts">
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import X from '@lucide/svelte/icons/x';

    import type { ProductTourListItem } from '../types';

    interface Props {
        busy?: boolean;
        onBrowse: () => Promise<void>;
        onDismiss: () => Promise<void>;
        onStart: () => Promise<void>;
        open?: boolean;
        recommended: ProductTourListItem;
    }

    let { busy = false, onBrowse, onDismiss, onStart, open = false, recommended }: Props = $props();

    async function onKeydown(event: KeyboardEvent): Promise<void> {
        if (event.key === 'Escape' && !busy) {
            event.stopPropagation();
            await onDismiss();
        }
    }
</script>

{#if open}
    <Alert.Root
        aria-label="Welcome to Exceptionless"
        aria-live="polite"
        class="motion-safe:animate-in motion-safe:fade-in-0 motion-safe:slide-in-from-bottom-2 fixed right-4 bottom-4 z-40 w-[min(24rem,calc(100vw-2rem))] p-4 shadow-lg motion-safe:duration-200"
        data-product-tour-welcome
        onkeydown={onKeydown}
        role="region"
    >
        <div class="flex items-center justify-between gap-2">
            <Alert.Title><h2>Welcome to Exceptionless</h2></Alert.Title>
            <Button aria-label="Close welcome" class="-my-2 -mr-2 size-11 shrink-0" disabled={busy} onclick={onDismiss} size="icon" variant="ghost">
                <X aria-hidden="true" />
            </Button>
        </div>
        <Alert.Description class="mt-1">{recommended.description}</Alert.Description>
        <div class="mt-3 flex flex-wrap items-center gap-2">
            <Button disabled={busy} onclick={onStart} size="sm">{recommended.name === 'project-configure' ? 'Continue setup' : recommended.title}</Button>
            <Button disabled={busy} onclick={onBrowse} size="sm" variant="ghost">Browse guides</Button>
        </div>
    </Alert.Root>
{/if}
