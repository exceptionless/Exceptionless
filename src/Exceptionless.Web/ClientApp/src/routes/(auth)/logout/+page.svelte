<script lang="ts">
    import { goto } from '$app/navigation';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import Loading from '$comp/Loading.svelte';
    import Logo from '$comp/Logo.svelte';
    import * as Card from '$comp/ui/card';
    import * as Form from '$comp/ui/form';
    import { accessToken, logout } from '$features/auth/index.svelte';
    import { useFetchClientStatus } from '$shared/api.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';

    let isAuthenticated = $derived(accessToken.value !== null);

    $effect(() => {
        if (!isAuthenticated) {
            goto('/next/login', { replaceState: true });
        }
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);

    let message = $state<string>();
    async function onLogout() {
        if (client.isLoading) {
            return;
        }

        const response = await client.get('auth/logout');
        if (response.ok) {
            await logout();
            await goto('/next/login');
        } else {
            message = 'An error occurred while logging out, please try again.';
        }
    }
</script>

<Card.Root class="mx-auto max-w-sm">
    <Card.Header>
        <Logo />
        <Card.Title class="text-center text-2xl">Log out?</Card.Title>
    </Card.Header>
    <Card.Content>
        <form onsubmit={onLogout}>
            <ErrorMessage {message}></ErrorMessage>

            <Form.Button>
                {#if clientStatus.isLoading}
                    <Loading class="mr-2" variant="secondary"></Loading> Logging out...
                {:else}
                    Logout
                {/if}
            </Form.Button>
        </form>
    </Card.Content>
</Card.Root>
