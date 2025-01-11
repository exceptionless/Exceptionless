<script lang="ts">
    interface Props {
        percent: null | number;
    }

    let { percent }: Props = $props();

    const formatOptions: Intl.NumberFormatOptions = {
        maximumFractionDigits: 1,
        minimumFractionDigits: 1,
        style: 'percent'
    };
</script>

{#if percent !== null && !isNaN(percent) && isFinite(percent)}
    {Intl.NumberFormat(undefined, formatOptions).format(percent / 100)}
{:else}
    {Intl.NumberFormat(undefined, formatOptions).format(0)}
{/if}
