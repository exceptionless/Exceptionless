<script lang="ts">
    import X from '@lucide/svelte/icons/x';

    import { Button } from '#comp/ui/button/index.ts';

    interface Props {
        onDismiss: () => void;
    }

    let { onDismiss }: Props = $props();
</script>

<Button
    type="button"
    variant="ghost"
    size="icon"
    class="size-5 rounded-sm p-0 opacity-70 hover:opacity-100"
    onclick={onDismiss}
    aria-label="Dismiss notification"
    title="Dismiss notification"
>
    <X class="size-4" aria-hidden="true" />
</Button>
