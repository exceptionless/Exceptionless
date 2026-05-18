<script lang="ts">
    import { Badge } from '$comp/ui/badge';

    interface Props {
        value: string;
    }

    let { value }: Props = $props();

    const knownTypeLabels: Record<string, string> = {
        '404': '404',
        error: 'Error',
        heartbeat: 'Heartbeat',
        log: 'Log',
        session: 'Session',
        sessionend: 'Session End',
        usage: 'Usage'
    };

    let label = $derived(knownTypeLabels[value] ?? value);
</script>

<Badge variant="outline">{label}</Badge>
