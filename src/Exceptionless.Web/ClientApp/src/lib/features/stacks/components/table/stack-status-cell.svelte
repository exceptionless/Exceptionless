<script lang="ts">
    import { Badge } from '$comp/ui/badge';

    interface Props {
        value: string;
    }

    let { value }: Props = $props();

    const statusLabels: Record<string, string> = {
        discarded: 'Discarded',
        fixed: 'Fixed',
        ignored: 'Ignored',
        open: 'Open',
        regressed: 'Regressed',
        snoozed: 'Snoozed'
    };

    const statusVariants: Record<string, 'default' | 'destructive' | 'outline' | 'secondary'> = {
        discarded: 'destructive',
        fixed: 'secondary',
        ignored: 'destructive',
        open: 'default',
        regressed: 'destructive',
        snoozed: 'secondary'
    };
</script>

<Badge variant={statusVariants[value] ?? 'outline'}>{statusLabels[value] ?? value}</Badge>
