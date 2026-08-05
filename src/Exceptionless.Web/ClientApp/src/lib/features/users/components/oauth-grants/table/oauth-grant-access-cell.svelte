<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import { Badge } from '$comp/ui/badge';
    import * as Tooltip from '$comp/ui/tooltip';

    interface Props {
        grant: OAuthGrant;
    }

    let { grant }: Props = $props();

    const scopeCount = $derived(new Set(grant.resources.flatMap((resource) => resource.scopes)).size);

    function formatResource(resource: string) {
        if (resource.endsWith('/mcp')) {
            return 'MCP';
        }

        if (resource.endsWith('/api/v2')) {
            return 'REST API';
        }

        return resource;
    }

    function formatResourceSummary() {
        if (grant.resources.length === 1) {
            return formatResource(grant.resources[0]!.resource);
        }

        return `${grant.resources.length} resources`;
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

<Tooltip.Root>
    <Tooltip.Trigger>
        {#snippet child({ props })}
            <button
                {...props}
                type="button"
                class="hover:text-primary focus-visible:ring-ring inline-flex max-w-full items-center gap-1 truncate rounded-sm text-left text-sm font-medium underline decoration-dotted underline-offset-4 focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none"
            >
                <span class="truncate">{formatResourceSummary()}</span>
                <span class="text-muted-foreground shrink-0">· {scopeCount} {scopeCount === 1 ? 'scope' : 'scopes'}</span>
            </button>
        {/snippet}
    </Tooltip.Trigger>
    <Tooltip.Content class="max-w-sm">
        <div class="space-y-3">
            {#each grant.resources as resource (resource.resource)}
                <div class="space-y-1">
                    <div class="text-sm font-medium">{formatResource(resource.resource)}</div>
                    <div class="flex flex-wrap gap-1">
                        {#each resource.scopes as scope (scope)}
                            <Badge variant={scope === 'stacks:write' ? 'amber' : 'secondary'}>{formatScope(scope)}</Badge>
                        {/each}
                    </div>
                </div>
            {/each}
        </div>
    </Tooltip.Content>
</Tooltip.Root>
