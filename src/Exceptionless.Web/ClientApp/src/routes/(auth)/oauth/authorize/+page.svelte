<script lang="ts">
    import { browser } from '$app/environment';
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import Logo from '$comp/logo.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Checkbox } from '$comp/ui/checkbox';
    import { Spinner } from '$comp/ui/spinner';
    import { accessToken } from '$features/auth/index.svelte';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';
    import { SvelteSet } from 'svelte/reactivity';

    interface OAuthAuthorizeResponse {
        error?: string;
        error_description?: string;
        redirect_uri?: string;
    }

    interface OAuthAuthorizeConsentResponse {
        client_id?: string;
        client_name?: string;
        error?: string;
        error_description?: string;
        redirect_uri?: string;
        required_scopes?: string[];
        resource?: string;
        scopes?: string[];
    }

    interface OAuthAuthorizeRequestBody {
        client_id: null | string;
        code_challenge: null | string;
        code_challenge_method: null | string;
        organization_ids: string[];
        redirect_uri: null | string;
        resource: null | string;
        response_type: null | string;
        scope: null | string;
        state: null | string;
    }

    const offlineAccessScope = 'offline_access';
    const mcpReadScope = 'mcp:read';

    let errorMessage = $state<null | string>(null);
    let consentDetails = $state<null | OAuthAuthorizeConsentResponse>(null);
    let consentErrorMessage = $state<null | string>(null);
    let isAuthorizing = $state(false);
    let isLoadingConsent = $state(false);
    let loadedConsentKey = $state<null | string>(null);
    let initializedOrganizationSelectionKey = $state<null | string>(null);
    const selectedOrganizationIds = new SvelteSet<string>();
    const selectedScopes = new SvelteSet<string>();

    const meQuery = getMeQuery();
    const organizationsQuery = getOrganizationsQuery({ params: { mode: null } });

    const clientId = $derived(page.url.searchParams.get('client_id') ?? 'Unknown application');
    const redirectUri = $derived(page.url.searchParams.get('redirect_uri') ?? 'Unknown redirect URI');
    const resource = $derived(page.url.searchParams.get('resource') ?? 'Unknown resource');
    const applicationClientId = $derived(consentDetails?.client_id ?? clientId);
    const applicationDisplayName = $derived(consentDetails?.client_name || 'Unknown application');
    const displayRedirectUri = $derived(consentDetails?.redirect_uri ?? redirectUri);
    const displayResource = $derived(consentDetails?.resource ?? resource);
    const accountDisplayName = $derived(meQuery.data?.full_name || meQuery.data?.email_address || 'Unknown account');
    const organizations = $derived(organizationsQuery.data?.data ?? []);
    const requestedScopes = $derived(consentDetails?.scopes ?? getRequestedScopes());
    const requiredScopes = $derived(consentDetails?.required_scopes ?? getRequiredScopes(displayResource));
    const missingRequiredScopes = $derived(requiredScopes.filter((scope) => !requestedScopes.includes(scope)));
    const requestedRequiredScopes = $derived(requestedScopes.filter((scope) => isRequiredScope(scope)));
    const requestedOptionalScopes = $derived(requestedScopes.filter((scope) => !isRequiredScope(scope)));
    const selectedScopeValues = $derived(getSelectedScopesInRequestOrder());
    const hasSelectedOrganizations = $derived(selectedOrganizationIds.size > 0);
    const hasSelectedResourceScope = $derived(selectedScopeValues.some((scope) => scope !== offlineAccessScope));
    const hasRequiredScopes = $derived(missingRequiredScopes.length === 0 && requiredScopes.every((scope) => selectedScopes.has(scope)));
    const canApprove = $derived(!isLoadingConsent && !consentErrorMessage && hasSelectedOrganizations && hasSelectedResourceScope && hasRequiredScopes);

    $effect(() => {
        if (!browser || accessToken.current) {
            return;
        }

        const returnUrl = `${page.url.pathname}${page.url.search}`;
        const loginUrl = `${resolve('/(auth)/login')}?redirect=${encodeURIComponent(returnUrl)}`;
        void goto(loginUrl, { replaceState: true });
    });

    $effect(() => {
        if (!browser || !accessToken.current) {
            return;
        }

        const consentKey = page.url.search;
        if (loadedConsentKey === consentKey) {
            return;
        }

        loadedConsentKey = consentKey;
        void loadConsentDetails();
    });

    $effect(() => {
        const organizationIds = organizations.map((organization) => organization.id).filter((id): id is string => Boolean(id));
        if (organizationIds.length === 0) {
            if (selectedOrganizationIds.size > 0) {
                selectedOrganizationIds.clear();
            }

            return;
        }

        const consentKey = page.url.search;
        if (initializedOrganizationSelectionKey !== consentKey) {
            selectedOrganizationIds.clear();
            for (const organizationId of organizationIds) {
                selectedOrganizationIds.add(organizationId);
            }

            initializedOrganizationSelectionKey = consentKey;
            return;
        }

        const validSelectedOrganizationIds = [...selectedOrganizationIds].filter((id) => organizationIds.includes(id));
        if (validSelectedOrganizationIds.length === selectedOrganizationIds.size) {
            return;
        }

        selectedOrganizationIds.clear();
        for (const organizationId of validSelectedOrganizationIds) {
            selectedOrganizationIds.add(organizationId);
        }
    });

    $effect(() => {
        selectedScopes.clear();
        for (const scope of requestedScopes) {
            selectedScopes.add(scope);
        }
    });

    async function loadConsentDetails(): Promise<void> {
        isLoadingConsent = true;
        consentErrorMessage = null;
        const client = useFetchClient();
        const response = await client.postJSON<OAuthAuthorizeConsentResponse>(
            'oauth/authorize/consent',
            getAuthorizationRequestBody([], page.url.searchParams.get('scope')),
            { expectedStatusCodes: [400, 401] }
        );

        isLoadingConsent = false;
        if (response.ok && response.data) {
            consentDetails = response.data;
            return;
        }

        if (response.status === 401) {
            await redirectToLogin();
            return;
        }

        consentDetails = null;
        consentErrorMessage =
            response.data?.error_description ||
            response.data?.error ||
            response.problem?.detail ||
            response.problem?.title ||
            'Unable to load application details.';
    }

    async function approveAuthorization(): Promise<void> {
        if (isAuthorizing) {
            return;
        }

        if (!hasSelectedOrganizations) {
            errorMessage = 'Select at least one organization.';
            return;
        }

        if (!hasRequiredScopes) {
            errorMessage = `Missing required scope: ${missingRequiredScopes.map(formatScope).join(', ')}.`;
            return;
        }

        if (!hasSelectedResourceScope) {
            errorMessage = 'Select at least one access scope.';
            return;
        }

        isAuthorizing = true;
        errorMessage = null;
        const client = useFetchClient();
        const response = await client.postJSON<OAuthAuthorizeResponse>(
            'oauth/authorize',
            getAuthorizationRequestBody([...selectedOrganizationIds], selectedScopeValues.join(' ')),
            { expectedStatusCodes: [400, 401] }
        );

        if (response.ok && response.data?.redirect_uri) {
            window.location.href = response.data.redirect_uri;
            return;
        }

        isAuthorizing = false;
        if (response.status === 401) {
            await redirectToLogin();
            return;
        }

        errorMessage =
            response.data?.error_description ||
            response.data?.error ||
            response.problem?.detail ||
            response.problem?.title ||
            'Unable to authorize application.';
    }

    async function redirectToLogin(): Promise<void> {
        accessToken.current = null;
        const returnUrl = `${page.url.pathname}${page.url.search}`;
        const loginUrl = `${resolve('/(auth)/login')}?redirect=${encodeURIComponent(returnUrl)}`;
        await goto(loginUrl, { replaceState: true });
    }

    function getAuthorizationRequestBody(organizationIds: string[], scope: null | string): OAuthAuthorizeRequestBody {
        return {
            client_id: page.url.searchParams.get('client_id'),
            code_challenge: page.url.searchParams.get('code_challenge'),
            code_challenge_method: page.url.searchParams.get('code_challenge_method'),
            organization_ids: organizationIds,
            redirect_uri: page.url.searchParams.get('redirect_uri'),
            resource: page.url.searchParams.get('resource'),
            response_type: page.url.searchParams.get('response_type'),
            scope,
            state: page.url.searchParams.get('state')
        };
    }

    function getRequestedScopes(): string[] {
        const scopes =
            page.url.searchParams
                .get('scope')
                ?.split(/\s+/)
                .map((scope) => scope.trim())
                .filter(Boolean) ?? [];

        return scopes;
    }

    function getRequiredScopes(resourceValue: string): string[] {
        if (resourceValue.endsWith('/mcp')) {
            return [mcpReadScope, offlineAccessScope];
        }

        return [];
    }

    function getSelectedScopesInRequestOrder(): string[] {
        return requestedScopes.filter((scope) => selectedScopes.has(scope));
    }

    function isRequiredScope(scope: string): boolean {
        return requiredScopes.includes(scope);
    }

    function toggleOrganization(organizationId: string | undefined, checked: 'indeterminate' | boolean): void {
        if (!organizationId) {
            return;
        }

        if (checked === true) {
            selectedOrganizationIds.add(organizationId);
        } else {
            selectedOrganizationIds.delete(organizationId);
        }
    }

    function toggleScope(scope: string, checked: 'indeterminate' | boolean): void {
        if (isRequiredScope(scope)) {
            selectedScopes.add(scope);
            return;
        }

        if (checked === true) {
            selectedScopes.add(scope);
        } else {
            selectedScopes.delete(scope);
        }
    }

    function formatScope(scope: string): string {
        switch (scope) {
            case 'events:read':
                return 'Events Read';
            case mcpReadScope:
                return 'MCP';
            case offlineAccessScope:
                return 'Offline Access';
            case 'projects:read':
                return 'Projects Read';
            case 'stacks:read':
                return 'Stacks Read';
            case 'stacks:write':
                return 'Stacks Write';
            default:
                return scope;
        }
    }

    function cancelAuthorization() {
        errorMessage = 'Authorization canceled. You can close this tab.';
    }
</script>

<div class="mx-auto flex w-full max-w-2xl flex-col items-center">
    <Card.Root class="w-full" size="sm">
        <Card.Header>
            <Logo class="max-h-10" />
            <Card.Title>Approve OAuth access</Card.Title>
            <Card.Description>Review the requested Exceptionless access before continuing.</Card.Description>
        </Card.Header>
        <Card.Content class="space-y-4">
            <div class="space-y-2 rounded-md border p-3 text-sm">
                <div>
                    <Muted>Signed in as</Muted>
                    {#if meQuery.isLoading}
                        <p class="text-muted-foreground">Loading account...</p>
                    {:else if meQuery.isError}
                        <p class="text-destructive">Unable to load account details.</p>
                    {:else}
                        <p class="truncate font-medium" title={accountDisplayName}>{accountDisplayName}</p>
                        <p class="text-muted-foreground truncate font-mono text-xs" title={meQuery.data?.email_address}>{meQuery.data?.email_address}</p>
                    {/if}
                </div>
                <div class="space-y-2">
                    <Muted>Organizations</Muted>
                    {#if organizationsQuery.isLoading}
                        <p class="text-muted-foreground">Loading organizations...</p>
                    {:else if organizationsQuery.isError}
                        <p class="text-destructive">Unable to load organizations.</p>
                    {:else if organizations.length > 0}
                        <div class="max-h-32 space-y-1 overflow-y-auto rounded-md border p-1">
                            {#each organizations as organization (organization.id)}
                                <label class="hover:bg-muted/50 flex min-h-8 items-center gap-2 rounded-sm px-2 text-sm">
                                    <Checkbox
                                        checked={selectedOrganizationIds.has(organization.id)}
                                        onCheckedChange={(checked) => toggleOrganization(organization.id, checked)}
                                    />
                                    <span class="min-w-0 flex-1 truncate font-medium">{organization.name}</span>
                                </label>
                            {/each}
                        </div>
                    {:else}
                        <p class="text-muted-foreground">This account is not a member of any organizations.</p>
                    {/if}
                </div>
            </div>

            <div class="grid gap-3 text-sm sm:grid-cols-3">
                <div class="min-w-0">
                    <Muted>Application</Muted>
                    {#if isLoadingConsent}
                        <p class="text-muted-foreground">Loading application...</p>
                    {:else}
                        <p class="truncate font-medium" title={applicationDisplayName}>{applicationDisplayName}</p>
                        {#if consentDetails?.client_name && applicationClientId !== applicationDisplayName}
                            <p class="text-muted-foreground truncate font-mono text-xs" title={applicationClientId}>{applicationClientId}</p>
                        {/if}
                    {/if}
                </div>
                <div class="min-w-0">
                    <Muted>Redirect URI</Muted>
                    <p class="truncate font-mono text-xs" title={displayRedirectUri}>{displayRedirectUri}</p>
                </div>
                <div class="min-w-0">
                    <Muted>Resource</Muted>
                    <p class="truncate font-mono text-xs" title={displayResource}>{displayResource}</p>
                </div>
            </div>

            <div class="space-y-2 text-sm">
                <Muted>Scopes</Muted>
                {#if requestedScopes.length > 0}
                    <div class="grid gap-2 sm:grid-cols-2">
                        {#each requestedRequiredScopes as scope (scope)}
                            <div class="bg-muted/30 flex min-h-12 items-center gap-2 rounded-sm border px-2 py-1.5 text-sm">
                                <span class="min-w-0 flex-1">
                                    <span class="flex min-w-0 flex-wrap items-center gap-1.5">
                                        <span class="truncate font-medium">{formatScope(scope)}</span>
                                        <Badge variant="outline">Required</Badge>
                                    </span>
                                    <span class="text-muted-foreground block truncate font-mono text-xs">{scope}</span>
                                </span>
                            </div>
                        {/each}
                        {#each requestedOptionalScopes as scope (scope)}
                            <label class="hover:bg-muted/50 flex min-h-12 items-center gap-2 rounded-sm border px-2 py-1.5 text-sm">
                                <Checkbox checked={selectedScopes.has(scope)} onCheckedChange={(checked) => toggleScope(scope, checked)} />
                                <span class="min-w-0 flex-1">
                                    <span class="block truncate font-medium">{formatScope(scope)}</span>
                                    <span class="text-muted-foreground block truncate font-mono text-xs">{scope}</span>
                                </span>
                            </label>
                        {/each}
                    </div>
                    {#if !hasSelectedResourceScope}
                        <p class="text-destructive text-xs">Select at least one access scope.</p>
                    {/if}
                    {#if missingRequiredScopes.length > 0}
                        <p class="text-destructive text-xs">Missing required scope: {missingRequiredScopes.map(formatScope).join(', ')}.</p>
                    {/if}
                {:else}
                    <p class="text-muted-foreground">No scopes requested.</p>
                {/if}
            </div>

            {#if errorMessage || consentErrorMessage}
                <ErrorMessage message={errorMessage ?? consentErrorMessage ?? ''}></ErrorMessage>
            {/if}
        </Card.Content>
        <Card.Footer class="flex justify-end gap-2">
            <Button type="button" variant="outline" onclick={cancelAuthorization} disabled={isAuthorizing}>Cancel</Button>
            <Button
                type="button"
                onclick={() => void approveAuthorization()}
                disabled={isAuthorizing ||
                    isLoadingConsent ||
                    Boolean(consentErrorMessage) ||
                    organizationsQuery.isLoading ||
                    organizationsQuery.isError ||
                    !canApprove}
            >
                {#if isAuthorizing}
                    <Spinner />
                    Authorizing...
                {:else}
                    Approve
                {/if}
            </Button>
        </Card.Footer>
    </Card.Root>
</div>
