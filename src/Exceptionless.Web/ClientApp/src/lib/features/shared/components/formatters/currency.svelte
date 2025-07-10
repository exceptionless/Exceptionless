<script lang="ts">
    interface Props {
        currency?: string;
        locale?: Intl.LocalesArgument;
        value: null | number;
    }

    let { currency = 'USD', locale = 'en-US', value }: Props = $props();
</script>

{#if value !== null && !isNaN(value) && isFinite(value)}
    {new Intl.NumberFormat(locale, {
        currency,
        style: 'currency'
    }).format(value)}
{/if}
