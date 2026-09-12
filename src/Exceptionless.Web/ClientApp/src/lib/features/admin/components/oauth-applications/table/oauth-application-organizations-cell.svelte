<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';

    import { resolve } from '$app/paths';
    import { Badge } from '$comp/ui/badge';

    let { application }: { application: OAuthApplication } = $props();
</script>

{#if application.organizations.length > 0}
    <div class="flex flex-wrap gap-1">
        {#each application.organizations as organization (organization.id)}
            <Badge
                href={organization.is_available
                    ? resolve('/(app)/organization/[organizationId]/manage', {
                          organizationId: organization.id
                      })
                    : undefined}
                variant="outline"
                class="max-w-56"
                title={organization.name}
            >
                <span class="truncate">{organization.name}</span>
            </Badge>
        {/each}
    </div>
{:else}
    <span class="text-muted-foreground text-xs">Not authorized</span>
{/if}
