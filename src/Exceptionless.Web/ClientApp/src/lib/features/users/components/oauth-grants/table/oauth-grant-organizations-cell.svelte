<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import { Badge } from '$comp/ui/badge';
    import * as Tooltip from '$comp/ui/tooltip';

    interface Props {
        grant: OAuthGrant;
        organizationNamesById: ReadonlyMap<string, string>;
    }

    let { grant, organizationNamesById }: Props = $props();

    const organizations = $derived(grant.organization_ids.map((id) => ({ id, name: formatOrganization(id) })));

    function formatOrganization(id: string) {
        return organizationNamesById.get(id) ?? id;
    }
</script>

{#if organizations.length > 0}
    <Tooltip.Root>
        <Tooltip.Trigger>
            {#snippet child({ props })}
                <button
                    {...props}
                    type="button"
                    class="flex max-w-full items-center gap-1 rounded-sm text-left focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
                >
                    <Badge class="max-w-40 truncate" variant="outline">{organizations[0]?.name}</Badge>
                    {#if organizations.length > 1}
                        <Badge variant="secondary">+{organizations.length - 1}</Badge>
                    {/if}
                </button>
            {/snippet}
        </Tooltip.Trigger>
        <Tooltip.Content class="max-w-xs">
            <div class="space-y-1">
                {#each organizations as organization (organization.id)}
                    <div class="truncate text-sm">{organization.name}</div>
                {/each}
            </div>
        </Tooltip.Content>
    </Tooltip.Root>
{:else}
    <span class="text-muted-foreground">-</span>
{/if}
