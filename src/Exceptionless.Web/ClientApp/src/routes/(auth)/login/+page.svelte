<script lang="ts">
    import IconFacebook from '~icons/mdi/facebook';
    import IconGitHub from '~icons/mdi/github';
    import IconGoogle from '~icons/mdi/google';
    import IconMicrosoft from '~icons/mdi/microsoft';

    import EmailInput from '$comp/form/EmailInput.svelte';
    import PasswordInput from '$comp/form/PasswordInput.svelte';

    import { goto } from '$app/navigation';
    import { page } from '$app/stores';
    import {
        login,
        liveLogin,
        facebookLogin,
        googleLogin,
        githubLogin,
        enableAccountCreation,
        googleClientId,
        enableOAuthLogin,
        facebookClientId,
        gitHubClientId,
        microsoftClientId
    } from '$api/auth.svelte';
    import { FetchClient, ProblemDetails } from '@exceptionless/fetchclient';
    import { Login } from '$lib/models/api';
    import Loading from '$comp/Loading.svelte';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { Button } from '$comp/ui/button';
    import { A, H2, Muted, P } from '$comp/typography';

    const data = new Login();

    // eslint-disable-next-line svelte/valid-compile
    data.invite_token = $page.url.searchParams.get('token');

    let problem = new ProblemDetails();
    const redirectUrl = $page.url.searchParams.get('redirect') ?? '/next';

    const { loading } = new FetchClient();
    async function onLogin() {
        if (loading) {
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

<form on:submit|preventDefault={onLogin} class="space-y-2">
    <ErrorMessage message={problem.errors.general}></ErrorMessage>

    <EmailInput name="email" bind:value={data.email} autocomplete="email" required {problem}></EmailInput>

    <PasswordInput
        name="password"
        bind:value={data.password}
        autocomplete="current-password"
        minlength={6}
        maxlength={100}
        required
        {problem}
        placeholder="Enter password"
    >
        <Muted slot="label" class="float-right">
            <A href="/forgot-password">Forgot password?</A>
        </Muted>
    </PasswordInput>

    <div class="pt-2">
        <Button type="submit">
            {#if loading}
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
