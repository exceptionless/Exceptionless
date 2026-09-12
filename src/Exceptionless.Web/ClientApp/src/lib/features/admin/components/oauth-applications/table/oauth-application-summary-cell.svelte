<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';

    import { resolve } from '$app/paths';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { A } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';

    interface Props {
        application: OAuthApplication;
    }

    let { application }: Props = $props();
</script>

<div class="min-w-0 space-y-1 whitespace-normal">
    <A
        href={resolve('/(app)/system/oauth-applications/[id=objectid]', {
            id: application.id
        })}
        class="font-medium"
        title={application.name}>{application.name}</A
    >
    <div class="text-muted-foreground text-xs">Updated <TimeAgo value={application.updated_utc} /></div>
    {#if application.is_disabled}
        <Badge variant="outline">Disabled</Badge>
    {/if}
</div>
