<script lang="ts">
    interface Props {
        formatOptions?: Omit<Intl.DateTimeFormatOptions, 'dateStyle' | 'timeStyle'>;
        value: Date | string | undefined;
    }

    let { formatOptions, value }: Props = $props();

    function formatDate(input: Date | string | undefined) {
        if (!input) {
            return '';
        }

        const date = typeof input === 'string' ? new Date(input) : input;
        if (isNaN(date.getTime())) {
            return '';
        }

        return date.toLocaleString(undefined, {
            day: 'numeric',
            hour: 'numeric',
            hour12: true,
            minute: '2-digit',
            month: 'short',
            second: '2-digit',
            year: 'numeric',
            ...formatOptions
        });
    }
</script>

{#if value}
    {formatDate(value)}
{/if}
