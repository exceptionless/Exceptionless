<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Badge } from '$comp/ui/badge';

    interface Props {
        grant: OAuthGrant;
    }

    let { grant }: Props = $props();
</script>

<div class="min-w-0 space-y-1 whitespace-normal">
    <div class="truncate font-medium" title={grant.application_name}>{grant.application_name}</div>
    <div class="text-xs text-muted-foreground">Updated <TimeAgo value={grant.updated_utc} /></div>
    {#if grant.is_application_disabled}
        <Badge variant="outline">Disabled</Badge>
    {/if}
</div>
