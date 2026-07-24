<script lang="ts">
    import BooleanFormatter from '$comp/formatters/boolean.svelte';

    interface Props {
        isCritical: boolean;
    }

    let { isCritical }: Props = $props();
</script>

<span class={isCritical ? 'font-medium text-destructive' : undefined}>
    <BooleanFormatter value={isCritical} />
</span>
