<script lang="ts">
    import {
        enableAccountCreation,
        enableOAuthLogin,
        facebookClientId,
        facebookLogin,
        gitHubClientId,
        githubLogin,
        googleClientId,
        googleLogin,
        liveLogin,
        login,
        microsoftClientId
    } from '$api/auth.svelte';
    import { goto } from '$app/navigation';
    import { page } from '$app/stores';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import EmailInput from '$comp/form/EmailInput.svelte';
    import PasswordInput from '$comp/form/PasswordInput.svelte';
    import Loading from '$comp/Loading.svelte';
    import { A, H2, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Login } from '$lib/models/api';
    import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
    import IconFacebook from '~icons/mdi/facebook';
    import IconGitHub from '~icons/mdi/github';
    import IconGoogle from '~icons/mdi/google';
    import IconMicrosoft from '~icons/mdi/microsoft';

    const data = $state(new Login());
    data.invite_token = $page.url.searchParams.get('token');

    const client = useFetchClient();
    let problem = $state(new ProblemDetails());
    const redirectUrl = $page.url.searchParams.get('redirect') ?? '/next';

    async function onLogin() {
        if (client.loading) {
            return;
        }

        let response = await login(data.email, data.password);
        if (response.ok) {
            await goto(redirectUrl);
        } else {
            problem = response.problem;
        }
    }
</script>

<H2 class="mb-2 mt-4 text-center leading-9">Log in to your account</H2>

<form class="space-y-2" onsubmit={onLogin}>
    <ErrorMessage message={problem.errors.general}></ErrorMessage>

    <EmailInput autocomplete="email" bind:value={data.email} name="email" {problem} required></EmailInput>

    <PasswordInput
        autocomplete="current-password"
        bind:value={data.password}
        maxlength={100}
        minlength={6}
        name="password"
        placeholder="Enter password"
        {problem}
        required
    >
        {#snippet labelChildren()}
            <Muted class="float-right">
                <A href="/forgot-password">Forgot password?</A>
            </Muted>
        {/snippet}
    </PasswordInput>

    <div class="pt-2">
        <Button type="submit">
            {#if client.loading}
                <Loading class="mr-2" variant="secondary"></Loading> Logging in...
            {:else}
                Login
            {/if}
        </Button>
    </div>
</form>

{#if enableOAuthLogin}
    <div class="my-4 flex w-full items-center">
        <hr class="w-full" />
        <P class="px-3">OR</P>
        <hr class="w-full" />
    </div>
    <div class="auto-cols-2 grid grid-flow-col grid-rows-2 gap-4">
        {#if microsoftClientId}
            <Button aria-label="Login with Microsoft" on:click={() => liveLogin(redirectUrl)}>
                <IconMicrosoft /> Microsoft
            </Button>
        {/if}
        {#if googleClientId}
            <Button aria-label="Login with Google" on:click={() => googleLogin(redirectUrl)}>
                <IconGoogle /> Google
            </Button>
        {/if}
        {#if facebookClientId}
            <Button aria-label="Login with Facebook" on:click={() => facebookLogin(redirectUrl)}>
                <IconFacebook /> Facebook
            </Button>
        {/if}
        {#if gitHubClientId}
            <Button aria-label="Login with GitHub" on:click={() => githubLogin(redirectUrl)}>
                <IconGitHub /> GitHub
            </Button>
        {/if}
    </div>
{/if}

{#if enableAccountCreation}
    <P class="text-center text-sm">
        Not a member?
        <A href="/signup">Start a free trial</A>
    </P>

    <P class="text-center text-sm">
        By signing up, you agree to our <A href="https://exceptionless.com/privacy" target="_blank">Privacy Policy</A>
        and
        <A href="https://exceptionless.com/terms" target="_blank">Terms of Service</A>.
    </P>
{/if}
