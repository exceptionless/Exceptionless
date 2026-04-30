<script lang="ts">
    interface Props {
        formatOptions?: Intl.NumberFormatOptions;
        value: null | number;
    }

    let { formatOptions, value }: Props = $props();
</script>

{#if value !== null && !isNaN(value) && isFinite(value)}
    {Intl.NumberFormat(undefined, formatOptions).format(value)}
{/if}
