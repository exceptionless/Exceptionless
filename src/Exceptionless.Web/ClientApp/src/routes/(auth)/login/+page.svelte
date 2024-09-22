<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/stores';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import Loading from '$comp/Loading.svelte';
    import { A, H2, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
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
    } from '$features/auth/index.svelte';
    import { Login } from '$features/auth/models';
    import { applyServerSideErrors } from '$features/shared/validation';
    import IconFacebook from '~icons/mdi/facebook';
    import IconGitHub from '~icons/mdi/github';
    import IconGoogle from '~icons/mdi/google';
    import IconMicrosoft from '~icons/mdi/microsoft';
    import { defaults, setMessage, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    const redirectUrl = $page.url.searchParams.get('redirect') ?? '/next';

    const defaultFormData = new Login();
    defaultFormData.invite_token = $page.url.searchParams.get('token');
    const form = superForm(defaults(defaultFormData, classvalidatorClient(Login)), {
        async onUpdate({ form }) {
            if (!form.valid) {
                console.log('Form is invalid');
                return;
            }

            let response = await login(form.data.email, form.data.password);
            if (response.ok) {
                console.log('Logged in');
                await goto(redirectUrl);
            } else {
                console.log('Failed to log in');
                applyServerSideErrors(form, response.problem);
            }
        },
        SPA: true,
        validators: classvalidatorClient(Login)
    });

    const { enhance, form: formData, message, submitting } = form;
</script>

<H2 class="mb-2 mt-4 text-center leading-9">Log in to your account</H2>

<form method="POST" use:enhance>
    <ErrorMessage message={$message}></ErrorMessage>
    <Form.Field {form} name="email">
        <Form.Control let:attrs>
            <Form.Label>Email</Form.Label>
            <Input {...attrs} bind:value={$formData.email} type="email" placeholder="Enter email address" autocomplete="email" required />
        </Form.Control>
        <Form.Description />
        <Form.FieldErrors />
    </Form.Field>
    <Form.Field {form} name="password">
        <Form.Control let:attrs>
            <Form.Label
                >Password
                <Muted class="float-right">
                    <A href="/forgot-password">Forgot password?</A>
                </Muted></Form.Label
            >
            <Input
                {...attrs}
                bind:value={$formData.password}
                type="password"
                placeholder="Enter password"
                autocomplete="current-password"
                maxlength={100}
                minlength={6}
                required
            />
        </Form.Control>
        <Form.Description />
        <Form.FieldErrors />
    </Form.Field>
    <Form.Button>
        {#if $submitting}
            <Loading class="mr-2" variant="secondary"></Loading> Logging in...
        {:else}
            Login
        {/if}</Form.Button
    >
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
