<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import { Badge } from '$comp/ui/badge';

    interface Props {
        grant: OAuthGrant;
        organizationNamesById: ReadonlyMap<string, string>;
    }

    let { grant, organizationNamesById }: Props = $props();

    function formatOrganization(id: string) {
        return organizationNamesById.get(id) ?? id;
    }

    function formatResource(resource: string) {
        if (resource.endsWith('/mcp')) {
            return 'MCP';
        }

        if (resource.endsWith('/api/v2')) {
            return 'REST API';
        }

        return resource;
    }

    function formatScope(scope: string) {
        switch (scope) {
            case 'events:read':
                return 'Events';
            case 'mcp:read':
                return 'MCP';
            case 'offline_access':
                return 'Offline';
            case 'projects:read':
                return 'Projects';
            case 'stacks:read':
                return 'Stacks';
            case 'stacks:write':
                return 'Stacks Write';
            default:
                return scope;
        }
    }
</script>

<div class="min-w-0 space-y-2 whitespace-normal">
    <div class="space-y-2">
        {#each grant.resources as resource (resource.resource)}
            <div class="min-w-0 space-y-1">
                <div class="truncate text-sm font-medium" title={resource.resource}>{formatResource(resource.resource)}</div>
                <div class="flex min-w-0 flex-wrap gap-1">
                    {#each resource.scopes as scope (scope)}
                        <Badge variant={scope === 'stacks:write' ? 'amber' : 'secondary'}>{formatScope(scope)}</Badge>
                    {/each}
                </div>
            </div>
        {/each}
    </div>

    <div class="min-w-0 space-y-1 border-t pt-2">
        <div class="text-xs font-medium text-muted-foreground">Organizations</div>
        {#if grant.organization_ids.length > 0}
            <div class="flex min-w-0 flex-wrap gap-1">
                {#each grant.organization_ids as organizationId (organizationId)}
                    <Badge class="max-w-full truncate" variant="outline" title={organizationId}>{formatOrganization(organizationId)}</Badge>
                {/each}
            </div>
        {:else}
            <span class="text-sm text-muted-foreground">-</span>
        {/if}
    </div>
</div>
