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
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { accessToken } from '$features/auth/index.svelte';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';
    import { SvelteSet } from 'svelte/reactivity';

    interface OAuthDeviceConsentResponse {
        client_id?: string;
        client_name?: string;
        error?: string;
        error_description?: string;
        required_scopes?: string[];
        resource?: string;
        scopes?: string[];
        user_code?: string;
    }

    const offlineAccessScope = 'offline_access';

    let consentDetails = $state<null | OAuthDeviceConsentResponse>(null);
    let errorMessage = $state<null | string>(null);
    let enteredUserCode = $state(page.url.searchParams.get('user_code') ?? '');
    let isApproving = $state(false);
    let isDenying = $state(false);
    let isLoadingConsent = $state(false);
    let loadedUserCode = $state<null | string>(null);
    let initializedOrganizationSelectionKey = $state<null | string>(null);
    let status = $state<'approved' | 'denied' | 'entry' | 'review'>('entry');
    const selectedOrganizationIds = new SvelteSet<string>();
    const selectedScopes = new SvelteSet<string>();

    const meQuery = getMeQuery();
    const organizationsQuery = getOrganizationsQuery({ params: { mode: null } });

    const accountDisplayName = $derived(meQuery.data?.full_name || meQuery.data?.email_address || 'Unknown account');
    const applicationClientId = $derived(consentDetails?.client_id ?? 'Unknown application');
    const applicationDisplayName = $derived(consentDetails?.client_name || applicationClientId);
    const organizations = $derived(organizationsQuery.data?.data ?? []);
    const requestedScopes = $derived(consentDetails?.scopes ?? []);
    const requiredScopes = $derived(consentDetails?.required_scopes ?? []);
    const requestedRequiredScopes = $derived(requestedScopes.filter((scope) => requiredScopes.includes(scope)));
    const requestedOptionalScopes = $derived(requestedScopes.filter((scope) => !requiredScopes.includes(scope)));
    const selectedScopeValues = $derived(requestedScopes.filter((scope) => selectedScopes.has(scope)));
    const hasRequiredScopes = $derived(requiredScopes.every((scope) => selectedScopes.has(scope)));
    const hasSelectedOrganizations = $derived(selectedOrganizationIds.size > 0);
    const hasSelectedResourceScope = $derived(selectedScopeValues.some((scope) => scope !== offlineAccessScope));
    const canApprove = $derived(status === 'review' && hasSelectedOrganizations && hasSelectedResourceScope && hasRequiredScopes && !isLoadingConsent);

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

        const userCode = page.url.searchParams.get('user_code');
        if (!userCode || loadedUserCode === userCode) {
            return;
        }

        enteredUserCode = userCode;
        void loadConsentDetails(userCode);
    });

    $effect(() => {
        const organizationIds = organizations.map((organization) => organization.id).filter((id): id is string => Boolean(id));
        if (organizationIds.length === 0 || !consentDetails?.user_code) {
            selectedOrganizationIds.clear();
            return;
        }

        if (initializedOrganizationSelectionKey === consentDetails.user_code) {
            return;
        }

        selectedOrganizationIds.clear();
        for (const organizationId of organizationIds) {
            selectedOrganizationIds.add(organizationId);
        }

        initializedOrganizationSelectionKey = consentDetails.user_code;
    });

    $effect(() => {
        selectedScopes.clear();
        for (const scope of requestedScopes) {
            selectedScopes.add(scope);
        }
    });

    async function loadConsentDetails(userCode: string): Promise<void> {
        const trimmedUserCode = userCode.trim();
        if (!trimmedUserCode) {
            errorMessage = 'Enter the code shown in your terminal.';
            return;
        }

        isLoadingConsent = true;
        errorMessage = null;
        loadedUserCode = trimmedUserCode;
        const client = useFetchClient();
        const response = await client.postJSON<OAuthDeviceConsentResponse>(
            'oauth/device/consent',
            { user_code: trimmedUserCode },
            { expectedStatusCodes: [400, 401] }
        );

        isLoadingConsent = false;
        if (response.ok && response.data) {
            consentDetails = response.data;
            enteredUserCode = response.data.user_code ?? trimmedUserCode;
            status = 'review';
            return;
        }

        if (response.status === 401) {
            await redirectToLogin();
            return;
        }

        consentDetails = null;
        status = 'entry';
        errorMessage =
            response.data?.error_description ||
            response.data?.error ||
            response.problem?.detail ||
            response.problem?.title ||
            'Unable to load device authorization.';
    }

    async function approveAuthorization(): Promise<void> {
        if (!consentDetails?.user_code || isApproving) {
            return;
        }

        if (!hasSelectedOrganizations) {
            errorMessage = 'Select at least one organization.';
            return;
        }

        if (!hasRequiredScopes) {
            errorMessage = `Missing required scope: ${requiredScopes.map(formatScope).join(', ')}.`;
            return;
        }

        if (!hasSelectedResourceScope) {
            errorMessage = 'Select at least one access scope.';
            return;
        }

        isApproving = true;
        errorMessage = null;
        const client = useFetchClient();
        const response = await client.postJSON(
            'oauth/device/authorize',
            {
                organization_ids: [...selectedOrganizationIds],
                scope: selectedScopeValues.join(' '),
                user_code: consentDetails.user_code
            },
            { expectedStatusCodes: [400, 401] }
        );

        isApproving = false;
        if (response.ok) {
            status = 'approved';
            return;
        }

        if (response.status === 401) {
            await redirectToLogin();
            return;
        }

        errorMessage = response.problem?.detail || response.problem?.title || 'Unable to authorize device.';
    }

    async function denyAuthorization(): Promise<void> {
        if (!consentDetails?.user_code || isDenying) {
            return;
        }

        isDenying = true;
        errorMessage = null;
        const client = useFetchClient();
        const response = await client.postJSON('oauth/device/deny', { user_code: consentDetails.user_code }, { expectedStatusCodes: [400, 401] });

        isDenying = false;
        if (response.ok) {
            status = 'denied';
            return;
        }

        if (response.status === 401) {
            await redirectToLogin();
            return;
        }

        errorMessage = response.problem?.detail || response.problem?.title || 'Unable to deny device authorization.';
    }

    async function redirectToLogin(): Promise<void> {
        accessToken.current = null;
        const returnUrl = `${page.url.pathname}${page.url.search}`;
        const loginUrl = `${resolve('/(auth)/login')}?redirect=${encodeURIComponent(returnUrl)}`;
        await goto(loginUrl, { replaceState: true });
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
        if (requiredScopes.includes(scope)) {
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
            case 'mcp:read':
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
</script>

<div class="mx-auto flex w-full max-w-2xl flex-col items-center">
    <Card.Root class="w-full" size="sm">
        <Card.Header>
            <Logo class="max-h-10" />
            <Card.Title>Authorize device</Card.Title>
            <Card.Description>Approve the Exceptionless access requested by your terminal session.</Card.Description>
        </Card.Header>

        {#if status === 'approved'}
            <Card.Content class="space-y-3">
                <div class="rounded-md border p-3 text-sm">
                    <p class="font-medium">Device authorized</p>
                    <p class="text-muted-foreground mt-1">Return to your terminal to continue.</p>
                </div>
            </Card.Content>
        {:else if status === 'denied'}
            <Card.Content class="space-y-3">
                <div class="rounded-md border p-3 text-sm">
                    <p class="font-medium">Device denied</p>
                    <p class="text-muted-foreground mt-1">The requesting terminal session will receive an access denied response.</p>
                </div>
            </Card.Content>
        {:else}
            <Card.Content class="space-y-4">
                <form
                    class="space-y-3"
                    onsubmit={(event) => {
                        event.preventDefault();
                        void loadConsentDetails(enteredUserCode);
                    }}
                >
                    <div class="space-y-2">
                        <label for="user-code" class="text-sm font-medium">Device code</label>
                        <div class="flex gap-2">
                            <Input
                                id="user-code"
                                class="font-mono uppercase"
                                value={enteredUserCode}
                                autocomplete="one-time-code"
                                oninput={(event) => {
                                    enteredUserCode = event.currentTarget.value;
                                }}
                            />
                            <Button type="submit" variant="outline" disabled={isLoadingConsent}>
                                {#if isLoadingConsent}
                                    <Spinner />
                                {:else}
                                    Continue
                                {/if}
                            </Button>
                        </div>
                    </div>
                </form>

                {#if consentDetails && status === 'review'}
                    <div class="space-y-2 rounded-md border p-3 text-sm">
                        <div>
                            <Muted>Signed in as</Muted>
                            {#if meQuery.isLoading}
                                <p class="text-muted-foreground">Loading account...</p>
                            {:else if meQuery.isError}
                                <p class="text-destructive">Unable to load account details.</p>
                            {:else}
                                <p class="truncate font-medium" title={accountDisplayName}>{accountDisplayName}</p>
                                <p class="truncate font-mono text-xs text-muted-foreground" title={meQuery.data?.email_address}>
                                    {meQuery.data?.email_address}
                                </p>
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
                                        <label class="flex min-h-8 items-center gap-2 rounded-sm px-2 text-sm hover:bg-muted/50">
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
                            <p class="truncate font-medium" title={applicationDisplayName}>{applicationDisplayName}</p>
                            {#if applicationClientId !== applicationDisplayName}
                                <p class="truncate font-mono text-xs text-muted-foreground" title={applicationClientId}>{applicationClientId}</p>
                            {/if}
                        </div>
                        <div class="min-w-0">
                            <Muted>Device Code</Muted>
                            <p class="truncate font-mono text-xs" title={consentDetails.user_code}>{consentDetails.user_code}</p>
                        </div>
                        <div class="min-w-0">
                            <Muted>Resource</Muted>
                            <p class="truncate font-mono text-xs" title={consentDetails.resource}>{consentDetails.resource}</p>
                        </div>
                    </div>

                    <div class="space-y-2 text-sm">
                        <Muted>Scopes</Muted>
                        <div class="grid gap-2 sm:grid-cols-2">
                            {#each requestedRequiredScopes as scope (scope)}
                                <div class="flex min-h-12 items-center gap-2 rounded-sm border bg-muted/30 px-2 py-1.5 text-sm">
                                    <span class="min-w-0 flex-1">
                                        <span class="flex min-w-0 flex-wrap items-center gap-1.5">
                                            <span class="truncate font-medium">{formatScope(scope)}</span>
                                            <Badge variant="outline">Required</Badge>
                                        </span>
                                        <span class="block truncate font-mono text-xs text-muted-foreground">{scope}</span>
                                    </span>
                                </div>
                            {/each}
                            {#each requestedOptionalScopes as scope (scope)}
                                <label class="flex min-h-12 items-center gap-2 rounded-sm border px-2 py-1.5 text-sm hover:bg-muted/50">
                                    <Checkbox checked={selectedScopes.has(scope)} onCheckedChange={(checked) => toggleScope(scope, checked)} />
                                    <span class="min-w-0 flex-1">
                                        <span class="block truncate font-medium">{formatScope(scope)}</span>
                                        <span class="block truncate font-mono text-xs text-muted-foreground">{scope}</span>
                                    </span>
                                </label>
                            {/each}
                        </div>
                        {#if !hasSelectedResourceScope}
                            <p class="text-destructive text-xs">Select at least one access scope.</p>
                        {/if}
                        {#if !hasRequiredScopes}
                            <p class="text-destructive text-xs">Missing required scope: {requiredScopes.map(formatScope).join(', ')}.</p>
                        {/if}
                    </div>
                {/if}

                {#if errorMessage}
                    <ErrorMessage message={errorMessage}></ErrorMessage>
                {/if}
            </Card.Content>

            {#if consentDetails && status === 'review'}
                <Card.Footer class="flex justify-end gap-2">
                    <Button type="button" variant="outline" onclick={() => void denyAuthorization()} disabled={isApproving || isDenying}>
                        {#if isDenying}
                            <Spinner />
                            Denying...
                        {:else}
                            Deny
                        {/if}
                    </Button>
                    <Button
                        type="button"
                        onclick={() => void approveAuthorization()}
                        disabled={isApproving || isDenying || organizationsQuery.isLoading || organizationsQuery.isError || !canApprove}
                    >
                        {#if isApproving}
                            <Spinner />
                            Authorizing...
                        {:else}
                            Approve
                        {/if}
                    </Button>
                </Card.Footer>
            {/if}
        {/if}
    </Card.Root>
</div>
