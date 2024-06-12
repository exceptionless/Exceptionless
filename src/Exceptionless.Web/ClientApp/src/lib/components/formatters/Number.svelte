<script lang="ts">
    interface Props {
        value: number | null;
    }

    let { value }: Props = $props();
</script>

{#if value !== null && !isNaN(value) && isFinite(value)}
    {Intl.NumberFormat().format(value)}
{/if}
