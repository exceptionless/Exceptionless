<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import { Badge } from '$comp/ui/badge';

    interface Props {
        grant: OAuthGrant;
    }

    let { grant }: Props = $props();

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

<div class="space-y-2">
    {#each grant.resources as resource (resource.resource)}
        <div class="grid gap-1 xl:grid-cols-[5rem_1fr] xl:items-start">
            <div class="truncate text-sm font-medium" title={resource.resource}>{formatResource(resource.resource)}</div>
            <div class="flex min-w-0 flex-wrap gap-1">
                {#each resource.scopes as scope (scope)}
                    <Badge variant={scope === 'stacks:write' ? 'amber' : 'secondary'}>{formatScope(scope)}</Badge>
                {/each}
            </div>
        </div>
    {/each}
</div>
