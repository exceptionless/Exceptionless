<script lang="ts">
    import { page } from '$app/state';
    import { Muted } from '$comp/typography';
    import * as Tabs from '$comp/ui/tabs';
    import OAuthApplicationsManager from '$features/admin/components/oauth-applications-manager.svelte';
    import AiToolsSetup from '$features/ai-tools/components/ai-tools-setup.svelte';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';

    const organizationId = $derived(page.params.organizationId || '');
    let selectedTab = $state('ai-tools');
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

    <Tabs.Root bind:value={selectedTab}>
        <Tabs.List>
            <Tabs.Trigger value="ai-tools">AI Tools</Tabs.Trigger>
            <Tabs.Trigger value="oauth-applications">OAuth Applications</Tabs.Trigger>
        </Tabs.List>

        <Tabs.Content value="ai-tools" class="mt-6">
            <AiToolsSetup />
        </Tabs.Content>

        <Tabs.Content value="oauth-applications" class="mt-6">
            <OAuthApplicationsManager
                description="Manage OAuth applications that can request access to this organization's Exceptionless data through the API and MCP tools."
                note="OAuth application definitions are currently global. Organization-scoped grants and connected-client revocation are tracked in the backend plan."
            />
        </Tabs.Content>
    </Tabs.Root>
</div>
