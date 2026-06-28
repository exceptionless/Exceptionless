<script lang="ts">
    import { page } from '$app/state';
    import { Muted } from '$comp/typography';
    import AiToolsSetup from '$features/ai-tools/components/ai-tools-setup.svelte';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';

    const organizationId = $derived(page.params.organizationId || '');
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });
</script>

<div class="space-y-6">
    <div class="space-y-1">
        <Muted>
            Connect AI tools and OAuth-based integrations for {organizationQuery.data?.name ?? 'this organization'}.
        </Muted>
    </div>

    <AiToolsSetup />
</div>
