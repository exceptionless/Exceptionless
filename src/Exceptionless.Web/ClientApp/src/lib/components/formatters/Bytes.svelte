<script lang="ts">
    interface Props {
        value: number | string | null;
    }

    let { value }: Props = $props();

    const parsedValue = typeof value === 'number' ? value : parseFloat(value ?? '');
    const byteValueNumberFormatter = new Intl.NumberFormat(navigator.language, {
        notation: 'compact',
        style: 'unit',
        unit: 'byte',
        unitDisplay: 'narrow'
    });
</script>

{#if !isNaN(parsedValue) && isFinite(parsedValue)}
    {byteValueNumberFormatter.format(parsedValue)}
{/if}
