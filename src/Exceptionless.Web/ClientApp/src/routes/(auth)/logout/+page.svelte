<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import Logo from '$comp/logo.svelte';
    import * as Card from '$comp/ui/card';
    import * as Form from '$comp/ui/form';
    import { Spinner } from '$comp/ui/spinner';
    import { accessToken, logout } from '$features/auth/index.svelte';
    import { useFetchClientStatus } from '$shared/api/api.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';

    let isAuthenticated = $derived(accessToken.current !== null);

    $effect(() => {
        if (!isAuthenticated) {
            goto(resolve('/(auth)/login'), { replaceState: true });
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
            await goto(resolve('/(auth)/login'));
        } else {
            message = 'An error occurred while logging out, please try again.';
        }
    }
</script>

<Card.Root class="mx-auto max-w-sm">
    <Card.Header class="min-w-[382px]">
        <Logo />
        <Card.Title class="text-center text-2xl">Log out?</Card.Title>
    </Card.Header>
    <Card.Content>
        <form onsubmit={onLogout}>
            <ErrorMessage {message}></ErrorMessage>

            <Form.Button>
                {#if clientStatus.isLoading}
                    <Spinner /> Logging out...
                {:else}
                    Logout
                {/if}
            </Form.Button>
        </form>
    </Card.Content>
</Card.Root>
