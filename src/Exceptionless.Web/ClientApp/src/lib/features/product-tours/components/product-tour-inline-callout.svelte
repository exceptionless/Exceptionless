<script lang="ts">
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import Info from '@lucide/svelte/icons/info';

    interface Props {
        description: string;
        onContinue?: () => void;
        onDismiss: () => void;
        title: string;
        tourId: string;
    }

    let { description, onContinue, onDismiss, title, tourId }: Props = $props();
</script>

<Alert.Root data-product-tour-inline={tourId}>
    <Info aria-hidden="true" />
    <Alert.Title>{title}</Alert.Title>
    <Alert.Description>
        {description}
        <div class="mt-2 flex flex-wrap gap-2">
            {#if onContinue}
                <Button onclick={onContinue} size="sm" type="button">Continue</Button>
            {/if}
            <Button onclick={onDismiss} size="sm" type="button" variant="outline">End guide</Button>
        </div>
    </Alert.Description>
</Alert.Root>
