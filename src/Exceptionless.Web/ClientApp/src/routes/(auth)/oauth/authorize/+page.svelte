<script lang="ts">
    import { browser } from '$app/environment';
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import Logo from '$comp/logo.svelte';
    import { Muted } from '$comp/typography';
    import * as Card from '$comp/ui/card';
    import { Spinner } from '$comp/ui/spinner';
    import { accessToken } from '$features/auth/index.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';

    interface OAuthAuthorizeResponse {
        redirect_uri: string;
    }

    let errorMessage = $state<null | string>(null);
    let started = false;

    $effect(() => {
        if (!browser || started) {
            return;
        }

        started = true;
        void completeAuthorization();
    });

    async function completeAuthorization(): Promise<void> {
        const returnUrl = `${page.url.pathname}${page.url.search}`;
        const loginUrl = `${resolve('/(auth)/login')}?redirect=${encodeURIComponent(returnUrl)}`;
        if (!accessToken.current) {
            await goto(loginUrl, { replaceState: true });
            return;
        }

        const client = useFetchClient();
        const response = await client.postJSON<OAuthAuthorizeResponse>(
            'oauth/authorize',
            {
                client_id: page.url.searchParams.get('client_id'),
                code_challenge: page.url.searchParams.get('code_challenge'),
                code_challenge_method: page.url.searchParams.get('code_challenge_method'),
                redirect_uri: page.url.searchParams.get('redirect_uri'),
                resource: page.url.searchParams.get('resource'),
                scope: page.url.searchParams.get('scope'),
                state: page.url.searchParams.get('state')
            },
            { expectedStatusCodes: [400, 401] }
        );

        if (response.ok && response.data?.redirect_uri) {
            window.location.href = response.data.redirect_uri;
            return;
        }

        if (response.status === 401) {
            accessToken.current = null;
            await goto(loginUrl, { replaceState: true });
            return;
        }

        errorMessage = response.problem?.detail || response.problem?.title || 'Unable to authorize application.';
    }
</script>

<div class="mx-auto flex w-[calc(100vw-2rem)] max-w-lg flex-col items-center">
    <Card.Root class="w-full">
        <Card.Header>
            <Logo />
            <Card.Title>Authorizing application</Card.Title>
            <Card.Description>Connecting your Exceptionless account.</Card.Description>
        </Card.Header>
        <Card.Content class="flex flex-col items-center gap-4 text-center">
            {#if errorMessage}
                <ErrorMessage message={errorMessage}></ErrorMessage>
            {:else}
                <Spinner class="size-8" />
                <Muted>Waiting for authorization to complete...</Muted>
            {/if}
        </Card.Content>
    </Card.Root>
</div>
