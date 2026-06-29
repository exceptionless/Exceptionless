<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import { Badge } from '$comp/ui/badge';

    interface Props {
        grant: OAuthGrant;
    }

    let { grant }: Props = $props();
</script>

<div class="min-w-0 space-y-1">
    <div class="truncate font-medium" title={grant.application_name}>{grant.application_name}</div>
    <div class="truncate font-mono text-xs text-muted-foreground" title={grant.client_id}>{grant.client_id}</div>
    {#if grant.is_application_disabled}
        <Badge variant="outline">Disabled</Badge>
    {/if}
</div>
