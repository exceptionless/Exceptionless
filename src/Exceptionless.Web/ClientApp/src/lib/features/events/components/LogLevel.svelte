<script lang="ts">
    import type { LogLevel } from '$features/events/models/event-data';

    import { Badge } from '$comp/ui/badge';

    interface Props {
        level?: LogLevel;
    }

    let { level }: Props = $props();

    // TODO: Add more variants;
    let variant: 'default' | 'destructive' | 'outline' | 'secondary' = $state('default');
    if (level === 'trace' || level === 'debug') variant = 'secondary';
    else if (level === 'info') variant = 'default';
    else if (level === 'warn') variant = 'destructive';
    else if (level === 'error') variant = 'destructive';
</script>

{#if level}
    <Badge {variant}>
        {level}
    </Badge>
{/if}
