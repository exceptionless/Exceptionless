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

    let errorMessage = $state<null | string>(null);
    let isAuthorizing = $state(false);
    const selectedOrganizationIds = new SvelteSet<string>();

    const meQuery = getMeQuery();
    const organizationsQuery = getOrganizationsQuery({ params: { mode: null } });

    const clientId = $derived(page.url.searchParams.get('client_id') ?? 'Unknown application');
    const redirectUri = $derived(page.url.searchParams.get('redirect_uri') ?? 'Unknown redirect URI');
    const resource = $derived(page.url.searchParams.get('resource') ?? 'Unknown resource');
    const accountDisplayName = $derived(meQuery.data?.full_name || meQuery.data?.email_address || 'Unknown account');
    const organizations = $derived(organizationsQuery.data?.data ?? []);
    const requestedScopes = $derived(getRequestedScopes());
    const hasSelectedOrganizations = $derived(selectedOrganizationIds.size > 0);

    $effect(() => {
        if (!browser || accessToken.current) {
            return;
        }

        const returnUrl = `${page.url.pathname}${page.url.search}`;
        const loginUrl = `${resolve('/(auth)/login')}?redirect=${encodeURIComponent(returnUrl)}`;
        void goto(loginUrl, { replaceState: true });
    });

    $effect(() => {
        const organizationIds = organizations.map((organization) => organization.id).filter((id): id is string => Boolean(id));
        if (organizationIds.length === 0) {
            if (selectedOrganizationIds.size > 0) {
                selectedOrganizationIds.clear();
            }

            return;
        }

        const validSelectedOrganizationIds = [...selectedOrganizationIds].filter((id) => organizationIds.includes(id));
        if (validSelectedOrganizationIds.length === selectedOrganizationIds.size && selectedOrganizationIds.size > 0) {
            return;
        }

        selectedOrganizationIds.clear();
        for (const organizationId of validSelectedOrganizationIds.length > 0 ? validSelectedOrganizationIds : organizationIds) {
            selectedOrganizationIds.add(organizationId);
        }
    });

    async function approveAuthorization(): Promise<void> {
        if (isAuthorizing) {
            return;
        }

        if (!hasSelectedOrganizations) {
            errorMessage = 'Select at least one organization.';
            return;
        }

        isAuthorizing = true;
        errorMessage = null;
        const client = useFetchClient();
        const response = await client.postJSON<OAuthAuthorizeResponse>(
            'oauth/authorize',
            {
                client_id: page.url.searchParams.get('client_id'),
                code_challenge: page.url.searchParams.get('code_challenge'),
                code_challenge_method: page.url.searchParams.get('code_challenge_method'),
                organization_ids: [...selectedOrganizationIds],
                redirect_uri: page.url.searchParams.get('redirect_uri'),
                resource: page.url.searchParams.get('resource'),
                response_type: page.url.searchParams.get('response_type'),
                scope: page.url.searchParams.get('scope'),
                state: page.url.searchParams.get('state')
            },
            { expectedStatusCodes: [400, 401] }
        );

        if (response.ok && response.data?.redirect_uri) {
            window.location.href = response.data.redirect_uri;
            return;
        }

        isAuthorizing = false;
        if (response.status === 401) {
            accessToken.current = null;
            const returnUrl = `${page.url.pathname}${page.url.search}`;
            const loginUrl = `${resolve('/(auth)/login')}?redirect=${encodeURIComponent(returnUrl)}`;
            await goto(loginUrl, { replaceState: true });
            return;
        }

        errorMessage =
            response.data?.error_description ||
            response.data?.error ||
            response.problem?.detail ||
            response.problem?.title ||
            'Unable to authorize application.';
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

    function cancelAuthorization() {
        errorMessage = 'Authorization canceled. You can close this tab.';
    }
</script>

<div class="mx-auto flex w-[calc(100vw-2rem)] max-w-xl flex-col items-center">
    <Card.Root class="w-full">
        <Card.Header>
            <Logo />
            <Card.Title>Approve OAuth access</Card.Title>
            <Card.Description>Review the requested Exceptionless access before continuing.</Card.Description>
        </Card.Header>
        <Card.Content class="space-y-5">
            <div class="space-y-3 rounded-md border p-3 text-sm">
                <div>
                    <Muted>Signed in as</Muted>
                    {#if meQuery.isLoading}
                        <p class="text-muted-foreground">Loading account...</p>
                    {:else if meQuery.isError}
                        <p class="text-destructive">Unable to load account details.</p>
                    {:else}
                        <p class="font-medium">{accountDisplayName}</p>
                        <p class="break-all font-mono text-xs text-muted-foreground">{meQuery.data?.email_address}</p>
                    {/if}
                </div>
                <div class="space-y-2">
                    <Muted>Organizations</Muted>
                    {#if organizationsQuery.isLoading}
                        <p class="text-muted-foreground">Loading organizations...</p>
                    {:else if organizationsQuery.isError}
                        <p class="text-destructive">Unable to load organizations.</p>
                    {:else if organizations.length > 0}
                        <div class="max-h-56 space-y-2 overflow-y-auto rounded-md border p-2">
                            {#each organizations as organization (organization.id)}
                                <label class="flex items-center gap-3 rounded-sm px-2 py-1.5 text-sm hover:bg-muted/50">
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

            <div class="space-y-3 text-sm">
                <div>
                    <Muted>Application</Muted>
                    <p class="break-all font-medium">{clientId}</p>
                </div>
                <div>
                    <Muted>Redirect URI</Muted>
                    <p class="break-all font-mono text-xs">{redirectUri}</p>
                </div>
                <div>
                    <Muted>Resource</Muted>
                    <p class="break-all font-mono text-xs">{resource}</p>
                </div>
                <div>
                    <Muted>Scopes</Muted>
                    <div class="mt-2 flex flex-wrap gap-1.5">
                        {#if requestedScopes.length > 0}
                            {#each requestedScopes as scope (scope)}
                                <Badge variant="secondary">{scope}</Badge>
                            {/each}
                        {:else}
                            <p class="text-muted-foreground">No scopes requested.</p>
                        {/if}
                    </div>
                </div>
            </div>

            {#if errorMessage}
                <ErrorMessage message={errorMessage}></ErrorMessage>
            {/if}
        </Card.Content>
        <Card.Footer class="flex justify-end gap-2">
            <Button type="button" variant="outline" onclick={cancelAuthorization} disabled={isAuthorizing}>Cancel</Button>
            <Button
                type="button"
                onclick={() => void approveAuthorization()}
                disabled={isAuthorizing || organizationsQuery.isLoading || organizationsQuery.isError || !hasSelectedOrganizations}
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
