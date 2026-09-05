<script lang="ts">
    import * as Typography from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import Sparkles from '@lucide/svelte/icons/sparkles';
    import X from '@lucide/svelte/icons/x';

    import ProductTourPrivacyLink from '../product-tour-privacy-link.svelte';

    interface Props {
        busy?: boolean;
        hasAccess: boolean;
        message?: string;
        onDismiss: () => void;
        onStart: () => void;
        open?: boolean;
    }

    let { busy = false, hasAccess, message, onDismiss, onStart, open = true }: Props = $props();
</script>

{#if open}
    <Alert.Root
        class="product-tour-feature-announcement fixed right-4 bottom-4 z-40 w-[min(24rem,calc(100vw-2rem))] shadow-lg"
        data-product-tour-announcement="exie"
    >
        <Sparkles aria-hidden="true" class="text-primary mt-0.5" />
        <div data-slot="alert-description">
            <div class="flex items-start justify-between gap-3">
                <div>
                    <Alert.Title>New: Meet Exie</Alert.Title>
                    <Typography.Muted class="mt-1">
                        {hasAccess
                            ? 'Take a short guide to Exie, your AI assistant for investigating errors.'
                            : (message ?? 'Exie is available with an eligible organization plan.')}
                    </Typography.Muted>
                </div>
                <Button aria-label="Dismiss Exie announcement" class="-mt-1 -mr-1 size-11" disabled={busy} onclick={onDismiss} size="icon" variant="ghost">
                    <X aria-hidden="true" class="size-4" />
                </Button>
            </div>
            <div class="mt-3 flex flex-wrap gap-2">
                <Button disabled={busy} onclick={onStart} size="sm">{hasAccess ? 'See how it works' : 'View access options'}</Button>
                <Button disabled={busy} onclick={onDismiss} size="sm" variant="outline">Dismiss</Button>
            </div>
            <ProductTourPrivacyLink />
        </div>
    </Alert.Root>
{/if}
