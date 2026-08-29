<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';

    import { Badge } from '$comp/ui/badge';
    import * as Tooltip from '$comp/ui/tooltip';

    interface Props {
        application: OAuthApplication;
    }

    let { application }: Props = $props();
</script>

{#if application.organizations.length > 0}
    <Tooltip.Root>
        <Tooltip.Trigger>
            {#snippet child({ props })}
                <button
                    {...props}
                    type="button"
                    class="focus-visible:ring-ring flex max-w-full items-center gap-1 rounded-sm text-left focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none"
                >
                    <Badge class="max-w-44 truncate" variant="outline">{application.organizations[0]?.name}</Badge>
                    {#if application.organizations.length > 1}
                        <Badge variant="secondary">+{application.organizations.length - 1}</Badge>
                    {/if}
                </button>
            {/snippet}
        </Tooltip.Trigger>
        <Tooltip.Content class="max-w-xs">
            <div class="space-y-1">
                {#each application.organizations as organization (organization.id)}
                    <div class="truncate text-sm">{organization.name}</div>
                {/each}
            </div>
        </Tooltip.Content>
    </Tooltip.Root>
{:else}
    <span class="text-muted-foreground text-xs">Not authorized</span>
{/if}
