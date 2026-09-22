<script lang="ts">
    interface Props {
        value: Date | string | undefined;
    }

    let { value }: Props = $props();

    function formatDate(input: Date | string | undefined) {
        if (!input) {
            return '';
        }

        const date = typeof input === 'string' ? new Date(input) : input;

        return date.toLocaleString(undefined, {
            day: 'numeric',
            hour: 'numeric',
            hour12: true,
            minute: '2-digit',
            month: 'short',
            second: '2-digit',
            year: 'numeric'
        });
    }
</script>

{#if value}
    {formatDate(value)}
{/if}
