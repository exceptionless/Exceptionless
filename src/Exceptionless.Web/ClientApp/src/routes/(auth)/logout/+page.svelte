<script lang="ts">
    import { useFetchClient, ProblemDetails } from '@exceptionless/fetchclient';
    import { goto } from '$app/navigation';
    import { accessToken, logout } from '$api/auth.svelte';
    import Loading from '$comp/Loading.svelte';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { Button } from '$comp/ui/button';
    import { H2 } from '$comp/typography';

    let isAuthenticated = $derived(accessToken.value !== null);
    $effect(() => {
        if (!isAuthenticated) {
            goto('/next/login', { replaceState: true });
        }
    });

    let problem = $state(new ProblemDetails());

    const { get, loading } = useFetchClient();
    async function onLogout() {
        if (loading) {
            return;
        }

        const response = await get('auth/logout');
        if (response.ok) {
            await logout();
            await goto('/next/login');
        } else {
            problem = problem.setErrorMessage('An error occurred while logging out, please try again.');
        }
    }
</script>

<H2 class="mb-2 mt-4 text-center leading-9">Log out?</H2>
<form on:submit|preventDefault={onLogout} class="space-y-2">
    <ErrorMessage message={problem.errors.general}></ErrorMessage>
    <div class="pt-2">
        <Button type="submit">
            {#if loading}
                <Loading></Loading> Logging out...
            {:else}
                Logout
            {/if}
        </Button>
    </div>
</form>
