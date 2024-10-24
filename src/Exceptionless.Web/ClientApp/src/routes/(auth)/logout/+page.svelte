<script lang="ts">
    import { goto } from '$app/navigation';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import Loading from '$comp/Loading.svelte';
    import { H2 } from '$comp/typography';
    import * as Form from '$comp/ui/form';
    import { accessToken, logout } from '$features/auth/index.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';

    let isAuthenticated = $derived(accessToken.value !== null);

    $effect(() => {
        if (!isAuthenticated) {
            goto('/next/login', { replaceState: true });
        }
    });

    const client = useFetchClient();
    let message = $state<string>();
    async function onLogout() {
        if (client.loading) {
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

<H2 class="mb-2 mt-4 text-center leading-9">Log out?</H2>
<form class="space-y-2" onsubmit={onLogout}>
    <ErrorMessage {message}></ErrorMessage>

    <Form.Button>
        {#if client.loading}
            <Loading class="mr-2" variant="secondary"></Loading> Logging out...
        {:else}
            Logout
        {/if}
    </Form.Button>
</form>
