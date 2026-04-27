<script lang="ts">
    interface Props {
        locale?: Intl.LocalesArgument;
        value: null | number;
    }

    let { locale = 'en-US', value }: Props = $props();
</script>

{#if value !== null && !isNaN(value) && isFinite(value)}
    {new Intl.NumberFormat(locale, { maximumFractionDigits: 1, notation: 'compact' }).format(value)}
{/if}
