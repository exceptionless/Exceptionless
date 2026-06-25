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
    import { Spinner } from '$comp/ui/spinner';
    import { accessToken } from '$features/auth/index.svelte';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';

    interface OAuthAuthorizeResponse {
        redirect_uri: string;
    }

    let errorMessage = $state<null | string>(null);
    let isAuthorizing = $state(false);

    const meQuery = getMeQuery();
    const organizationsQuery = getOrganizationsQuery({ params: { mode: null } });

    const clientId = $derived(page.url.searchParams.get('client_id') ?? 'Unknown application');
    const redirectUri = $derived(page.url.searchParams.get('redirect_uri') ?? 'Unknown redirect URI');
    const resource = $derived(page.url.searchParams.get('resource') ?? 'Unknown resource');
    const accountDisplayName = $derived(meQuery.data?.full_name || meQuery.data?.email_address || 'Unknown account');
    const organizations = $derived(organizationsQuery.data?.data ?? []);
    const visibleOrganizations = $derived(organizations.slice(0, 6));
    const hiddenOrganizationCount = $derived(Math.max(organizations.length - visibleOrganizations.length, 0));
    const requestedScopes = $derived(
        page.url.searchParams
            .get('scope')
            ?.split(/\s+/)
            .map((scope) => scope.trim())
            .filter(Boolean) ?? []
    );

    $effect(() => {
        if (!browser || accessToken.current) {
            return;
        }

        const returnUrl = `${page.url.pathname}${page.url.search}`;
        const loginUrl = `${resolve('/(auth)/login')}?redirect=${encodeURIComponent(returnUrl)}`;
        void goto(loginUrl, { replaceState: true });
    });

    async function approveAuthorization(): Promise<void> {
        if (isAuthorizing) {
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

        errorMessage = response.problem?.detail || response.problem?.title || 'Unable to authorize application.';
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
                <div>
                    <Muted>Organizations</Muted>
                    {#if organizationsQuery.isLoading}
                        <p class="text-muted-foreground">Loading organizations...</p>
                    {:else if organizationsQuery.isError}
                        <p class="text-destructive">Unable to load organizations.</p>
                    {:else if organizations.length > 0}
                        <p class="text-muted-foreground">Access applies to these organizations.</p>
                        <div class="mt-2 flex flex-wrap gap-1.5">
                            {#each visibleOrganizations as organization (organization.id)}
                                <Badge variant="outline">{organization.name}</Badge>
                            {/each}
                            {#if hiddenOrganizationCount > 0}
                                <Badge variant="secondary">+{hiddenOrganizationCount} more</Badge>
                            {/if}
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
                        {#each requestedScopes as scope (scope)}
                            <Badge variant="secondary">{scope}</Badge>
                        {:else}
                            <Badge variant="outline">Default MCP read access</Badge>
                        {/each}
                    </div>
                </div>
            </div>

            {#if errorMessage}
                <ErrorMessage message={errorMessage}></ErrorMessage>
            {/if}
        </Card.Content>
        <Card.Footer class="flex justify-end gap-2">
            <Button type="button" variant="outline" onclick={cancelAuthorization} disabled={isAuthorizing}>Cancel</Button>
            <Button type="button" onclick={() => void approveAuthorization()} disabled={isAuthorizing}>
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
