<script lang="ts">
    import { FetchClient, ProblemDetails } from '$api/FetchClient';
    import { goto } from '$app/navigation';
    import { isAuthenticated, logout } from '$api/auth';
    import Loading from '$comp/Loading.svelte';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { Button } from '$comp/ui/button';
    import H2 from '$comp/typography/H2.svelte';

    $: if (!$isAuthenticated) {
        goto('/next/login', { replaceState: true });
    }

    let problem = new ProblemDetails();

    const { get, loading } = new FetchClient();
    async function onLogout() {
        if ($loading) {
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
            {#if $loading}
                <Loading></Loading> Logging out...
            {:else}
                Logout
            {/if}
        </Button>
    </div>
</form>
