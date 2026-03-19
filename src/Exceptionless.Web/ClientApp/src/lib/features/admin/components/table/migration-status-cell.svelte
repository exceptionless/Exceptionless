<script lang="ts">
    import type { MigrationStatus } from '$features/admin/models';

    import CheckCircle2 from '@lucide/svelte/icons/check-circle-2';
    import Clock from '@lucide/svelte/icons/clock';
    import Loader from '@lucide/svelte/icons/loader';
    import XCircle from '@lucide/svelte/icons/x-circle';

    interface Props {
        status: MigrationStatus;
    }

    let { status }: Props = $props();

    const styles = {
        Completed: { colorClass: 'text-muted-foreground', icon: CheckCircle2 },
        Failed: { colorClass: 'text-destructive', icon: XCircle },
        Pending: { colorClass: 'text-amber-500', icon: Clock },
        Running: { colorClass: 'text-blue-500', icon: Loader }
    } as const;

    const style = $derived(styles[status]);
</script>

<div class="flex items-center gap-1.5">
    <style.icon class="size-3.5 {style.colorClass} {status === 'Running' ? 'animate-spin' : ''}" />
    <span class="text-xs {style.colorClass}">{status}</span>
</div>
