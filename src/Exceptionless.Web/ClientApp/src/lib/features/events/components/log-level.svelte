<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { getLogLevel, type LogLevel } from '$features/events/models/event-data';

    interface Props {
        level?: LogLevel;
    }

    let { level }: Props = $props();

    function getLogLevelVariant(level: LogLevel | null): 'default' | 'destructive' | 'outline' | 'secondary' {
        if (level === 'trace' || level === 'debug') {
            return 'secondary';
        }

        if (level === 'info') {
            return 'default';
        }

        if (level === 'warn' || level === 'error') {
            return 'destructive';
        }

        return 'default';
    }

    const normalizedLogLevel = $derived(getLogLevel(level));
    const variant = $derived(getLogLevelVariant(normalizedLogLevel));
</script>

{#if normalizedLogLevel}
    <Badge {variant}>
        {normalizedLogLevel}
    </Badge>
{/if}
