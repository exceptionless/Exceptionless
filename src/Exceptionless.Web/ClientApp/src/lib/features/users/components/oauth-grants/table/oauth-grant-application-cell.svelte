<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import DateTime from '$comp/formatters/date-time.svelte';
    import { Badge } from '$comp/ui/badge';

    interface Props {
        grant: OAuthGrant;
    }

    let { grant }: Props = $props();
</script>

<div class="min-w-0 space-y-1 whitespace-normal">
    <div class="truncate font-medium" title={grant.application_name}>{grant.application_name}</div>
    <div class="line-clamp-2 break-all font-mono text-xs text-muted-foreground" title={grant.client_id}>{grant.client_id}</div>
    <div class="text-xs text-muted-foreground">Updated <DateTime value={grant.updated_utc} /></div>
    {#if grant.is_application_disabled}
        <Badge variant="outline">Disabled</Badge>
    {/if}
</div>
