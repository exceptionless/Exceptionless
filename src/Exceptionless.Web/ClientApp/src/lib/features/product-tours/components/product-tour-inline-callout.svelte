<script lang="ts">
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import Info from '@lucide/svelte/icons/info';

    interface Props {
        continueLabel?: string;
        description: string;
        onContinue?: () => Promise<void> | void;
        onDismiss: () => Promise<void> | void;
        title: string;
        tourName: string;
    }

    let { continueLabel = 'Continue', description, onContinue, onDismiss, title, tourName }: Props = $props();
</script>

<Alert.Root data-product-tour-inline={tourName}>
    <Info aria-hidden="true" />
    <Alert.Title>{title}</Alert.Title>
    <Alert.Description>
        {description}
        <div class="mt-2 flex flex-wrap gap-2">
            {#if onContinue}
                <Button onclick={onContinue} size="sm" type="button">{continueLabel}</Button>
            {/if}
            <Button onclick={onDismiss} size="sm" type="button" variant="outline">End guide</Button>
        </div>
    </Alert.Description>
</Alert.Root>
