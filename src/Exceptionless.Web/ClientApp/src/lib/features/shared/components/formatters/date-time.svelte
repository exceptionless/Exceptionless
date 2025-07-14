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
            month: 'short',
            day: 'numeric',
            year: 'numeric',
            hour: 'numeric',
            minute: '2-digit',
            second: '2-digit',
            hour12: true
        });
    }
</script>

{#if value}
    {formatDate(value)}
{/if}
