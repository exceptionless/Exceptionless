<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';

    import { Badge } from '$comp/ui/badge';
    import * as Tooltip from '$comp/ui/tooltip';

    interface Props {
        application: OAuthApplication;
    }

    let { application }: Props = $props();
</script>

<Tooltip.Root>
    <Tooltip.Trigger>
        {#snippet child({ props })}
            <button
                {...props}
                type="button"
                class="focus-visible:ring-ring rounded-sm focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none"
            >
                <Badge variant="secondary">{application.scopes.length} {application.scopes.length === 1 ? 'scope' : 'scopes'}</Badge>
            </button>
        {/snippet}
    </Tooltip.Trigger>
    <Tooltip.Content class="max-w-xs">
        <div class="flex flex-wrap gap-1">
            {#each application.scopes as scope (scope)}
                <Badge variant={scope === 'stacks:write' ? 'amber' : 'secondary'}>{scope}</Badge>
            {/each}
        </div>
    </Tooltip.Content>
</Tooltip.Root>
