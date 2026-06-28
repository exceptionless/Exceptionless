<script lang="ts">
    import { page } from '$app/state';
    import { Muted } from '$comp/typography';
    import * as Tabs from '$comp/ui/tabs';
    import OAuthApplicationsManager from '$features/admin/components/oauth-applications-manager.svelte';
    import McpSetup from '$features/mcp/components/mcp-setup.svelte';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { queryParamsState } from 'kit-query-params';

    const organizationId = $derived(page.params.organizationId || '');
    type IntegrationsTab = 'mcp' | 'oauth-applications';

    const DEFAULT_PARAMS = {
        tab: 'mcp' as IntegrationsTab
    };
    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            tab: '<mcp,oauth-applications>'
        }
    });
    const selectedTab = $derived(queryParams.tab ?? DEFAULT_PARAMS.tab);
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    $effect(() => {
        queryParams.tab ??= DEFAULT_PARAMS.tab;
    });

    function handleTabChange(value: string) {
        if (value === 'mcp' || value === 'oauth-applications') {
            queryParams.tab = value;
        }
    }
</script>

<div class="space-y-6">
    <div class="space-y-1">
        <Muted>
            Connect MCP clients and OAuth-based integrations for {organizationQuery.data?.name ?? 'this organization'}.
        </Muted>
    </div>

    <Tabs.Root value={selectedTab} onValueChange={handleTabChange}>
        <Tabs.List>
            <Tabs.Trigger value="mcp">MCP</Tabs.Trigger>
            <Tabs.Trigger value="oauth-applications">OAuth Applications</Tabs.Trigger>
        </Tabs.List>

        <Tabs.Content value="mcp" class="mt-6">
            <McpSetup />
        </Tabs.Content>

        <Tabs.Content value="oauth-applications" class="mt-6">
            <OAuthApplicationsManager
                description="Manage OAuth applications that can request access to this organization's Exceptionless data through the API and MCP tools."
                note="OAuth application definitions are currently global. Organization-scoped grants and connected-client revocation are tracked in the backend plan."
            />
        </Tabs.Content>
    </Tabs.Root>
</div>
