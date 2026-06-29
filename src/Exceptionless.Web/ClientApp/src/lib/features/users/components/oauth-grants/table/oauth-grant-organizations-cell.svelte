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
</script>

{#if grant.organization_ids.length > 0}
    <div class="flex min-w-0 flex-wrap gap-1">
        {#each grant.organization_ids as organizationId (organizationId)}
            <Badge variant="outline" title={organizationId}>{formatOrganization(organizationId)}</Badge>
        {/each}
    </div>
{:else}
    <span class="text-muted-foreground">-</span>
{/if}
