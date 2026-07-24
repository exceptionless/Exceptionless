<script lang="ts">
    interface Props {
        value: null | number | string;
    }

    let { value }: Props = $props();

    const parsedValue = $derived(typeof value === 'number' ? value : parseFloat(value ?? ''));
    const byteUnits: { divisor: number; unit: Intl.NumberFormatOptions['unit'] }[] = [
        { divisor: 1e12, unit: 'terabyte' },
        { divisor: 1e9, unit: 'gigabyte' },
        { divisor: 1e6, unit: 'megabyte' },
        { divisor: 1e3, unit: 'kilobyte' },
        { divisor: 1, unit: 'byte' }
    ];
    const byteUnit = $derived(byteUnits.find(({ divisor }) => Math.abs(parsedValue) >= divisor) ?? byteUnits.at(-1)!);
    const byteValueNumberFormatter = $derived(
        new Intl.NumberFormat(navigator.language, {
            maximumFractionDigits: 0,
            style: 'unit',
            unit: byteUnit.unit,
            unitDisplay: 'short'
        })
    );
</script>

{#if !isNaN(parsedValue) && isFinite(parsedValue)}
    {byteValueNumberFormatter.format(parsedValue / byteUnit.divisor)}
{/if}
