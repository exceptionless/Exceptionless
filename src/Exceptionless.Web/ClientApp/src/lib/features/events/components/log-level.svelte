<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { type LogLevel } from '$features/events/models/event-data';

    import { getLogLevel } from '../utils';

    interface Props {
        level?: LogLevel;
    }

    let { level }: Props = $props();

    function getLogLevelVariant(level: LogLevel | null): 'default' | 'destructive' | 'info' | 'outline' | 'secondary' | 'yellow' {
        if (level === 'trace' || level === 'debug') {
            return 'secondary';
        }

        if (level === 'info') {
            return 'info';
        }

        if (level === 'warn') {
            return 'yellow';
        }

        if (level === 'error' || level === 'fatal') {
            return 'destructive';
        }

        return 'default';
    }

    function getDarkThemeClasses(level: LogLevel | null): string {
        if (level === 'error' || level === 'fatal') {
            return 'dark:border-red-400/30 dark:bg-red-500/10 dark:text-red-300';
        }

        if (level === 'info') {
            return 'dark:border-green-400/30 dark:bg-green-500/10 dark:text-green-200';
        }

        if (level === 'warn') {
            return 'dark:border-yellow-400/30 dark:bg-yellow-500/10 dark:text-yellow-200';
        }

        return 'dark:border-white/20 dark:bg-white/5 dark:text-zinc-200';
    }

    const normalizedLogLevel = $derived(getLogLevel(level));
    const darkThemeClasses = $derived(getDarkThemeClasses(normalizedLogLevel));
    const variant = $derived(getLogLevelVariant(normalizedLogLevel));
</script>

{#if normalizedLogLevel}
    <Badge class={['w-12 justify-center rounded-md border border-current/20 px-0 text-center', darkThemeClasses]} {variant}>
        {normalizedLogLevel}
    </Badge>
{/if}
