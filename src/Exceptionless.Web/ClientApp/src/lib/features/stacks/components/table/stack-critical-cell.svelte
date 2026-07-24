<script lang="ts">
    import BooleanFormatter from '$comp/formatters/boolean.svelte';

    interface Props {
        isCritical: boolean;
    }

    let { isCritical }: Props = $props();
</script>

<span class={isCritical ? 'text-destructive font-medium' : undefined}>
    <BooleanFormatter value={isCritical} />
</span>
