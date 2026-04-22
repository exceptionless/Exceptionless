<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import Logo from '$comp/logo.svelte';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Spinner } from '$comp/ui/spinner';
    import { accessToken, logout } from '$features/auth/index.svelte';
    import { useFetchClientStatus } from '$shared/api/api.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';

    let isAuthenticated = $derived(!!accessToken.current);

    $effect(() => {
        if (!isAuthenticated) {
            goto(resolve('/(auth)/login'), { replaceState: true });
        }
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    const queryClient = useQueryClient();

    let message = $state<string>();
    async function onLogout(event: SubmitEvent) {
        event.preventDefault();

        if (client.isLoading) {
            return;
        }

        await logout(queryClient, client);
        await goto(resolve('/(auth)/login'));
    }
</script>

<Card.Root class="mx-auto w-sm">
    <Card.Header class="min-w-95.5">
        <Logo />
    </Card.Header>
    <Card.Content>
        <form onsubmit={onLogout}>
            <ErrorMessage {message}></ErrorMessage>

            <Button type="submit" class="w-full">
                {#if clientStatus.isLoading}
                    <Spinner /> Logging out...
                {:else}
                    Logout
                {/if}
            </Button>
        </form>
    </Card.Content>
</Card.Root>
