<script lang="ts">
    import { goto } from '$app/navigation';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import Loading from '$comp/Loading.svelte';
    import { H2 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { accessToken, logout } from '$features/auth/index.svelte';
    import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';

    let isAuthenticated = $derived(accessToken.value !== null);
    $effect(() => {
        if (!isAuthenticated) {
            goto('/next/login', { replaceState: true });
        }
    });

    const client = useFetchClient();
    let problem = $state(new ProblemDetails());
    async function onLogout() {
        if (client.loading) {
            return;
        }

        const response = await client.get('auth/logout');
        if (response.ok) {
            await logout();
            await goto('/next/login');
        } else {
            problem = problem.setErrorMessage('An error occurred while logging out, please try again.');
        }
    }
</script>

<H2 class="mb-2 mt-4 text-center leading-9">Log out?</H2>
<form class="space-y-2" onsubmit={onLogout}>
    <ErrorMessage message={problem.errors.general}></ErrorMessage>
    <div class="pt-2">
        <Button type="submit">
            {#if client.loading}
                <Loading></Loading> Logging out...
            {:else}
                Logout
            {/if}
        </Button>
    </div>
</form>
