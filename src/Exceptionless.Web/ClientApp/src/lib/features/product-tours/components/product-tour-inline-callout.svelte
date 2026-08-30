<script lang="ts">
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import Info from '@lucide/svelte/icons/info';

    import type { ProductTourCheckpoint } from '../types';

    import { PRODUCT_TOUR_CHECKPOINTS } from '../types';

    interface Props {
        checkpoint: ProductTourCheckpoint;
        continueLabel?: string;
        description: string;
        onContinue?: () => Promise<void> | void;
        onDismiss: () => Promise<void> | void;
        title: string;
    }

    let { checkpoint, continueLabel = 'Continue', description, onContinue, onDismiss, title }: Props = $props();
    const checkpoints = $derived<readonly string[]>(PRODUCT_TOUR_CHECKPOINTS[checkpoint.tourName]);
    const stepNumber = $derived(checkpoints.indexOf(checkpoint.checkpointName) + 1);
</script>

<Alert.Root data-product-tour-inline={checkpoint.tourName}>
    <Info aria-hidden="true" />
    <Alert.Title>{title}</Alert.Title>
    <Alert.Description>
        <span class="text-muted-foreground mb-1 block text-xs">Step {stepNumber} of {checkpoints.length}</span>
        {description}
        <div class="mt-2 flex flex-wrap gap-2">
            {#if onContinue}
                <Button onclick={onContinue} size="sm" type="button">{continueLabel}</Button>
            {/if}
            <Button onclick={onDismiss} size="sm" type="button" variant="outline">End guide</Button>
        </div>
    </Alert.Description>
</Alert.Root>
