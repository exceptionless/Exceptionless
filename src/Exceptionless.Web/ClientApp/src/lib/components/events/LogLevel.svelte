<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import type { LogLevel } from '$lib/models/client-data';

    export let level: LogLevel | undefined;

    // TODO: Add more variants;
    let variant: 'default' | 'secondary' | 'destructive' | 'outline' = 'default';
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
