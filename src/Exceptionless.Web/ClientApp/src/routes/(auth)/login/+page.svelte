<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import GoogleIcon from '$comp/icons/GoogleIcon.svelte';
    import MicrosoftIcon from '$comp/icons/MicrosoftIcon.svelte';
    import Loading from '$comp/loading.svelte';
    import Logo from '$comp/logo.svelte';
    import { A, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
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
    import { applyServerSideErrors } from '$shared/validation';
    import Facebook from '@lucide/svelte/icons/facebook';
    import GitHub from '@lucide/svelte/icons/github';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    const redirectUrl = page.url.searchParams.get('redirect') ?? '/next';

    const defaultFormData = new Login();
    defaultFormData.invite_token = page.url.searchParams.get('token');
    const form = superForm(defaults(defaultFormData, classvalidatorClient(Login)), {
        dataType: 'json',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            let response = await login(form.data.email, form.data.password);
            if (response.ok) {
                await goto(redirectUrl);
            } else {
                applyServerSideErrors(form, response.problem);
                result.status = response.problem.status ?? 500;
            }
        },
        SPA: true,
        validators: classvalidatorClient(Login)
    });

    const { enhance, form: formData, message, submitting } = form;
</script>

<Card.Root class="mx-auto max-w-sm">
    <Card.Header>
        <Logo />
        <Card.Title class="text-center text-2xl">Log in to your account</Card.Title>
    </Card.Header>
    <Card.Content>
        <form method="POST" use:enhance>
            <ErrorMessage message={$message}></ErrorMessage>
            <Form.Field {form} name="email">
                <Form.Control>
                    {#snippet children({ props })}
                        <Form.Label>Email</Form.Label>
                        <Input {...props} bind:value={$formData.email} type="email" placeholder="Enter email address" autocomplete="email" required />
                    {/snippet}
                </Form.Control>
                <Form.Description />
                <Form.FieldErrors />
            </Form.Field>
            <Form.Field {form} name="password">
                <Form.Control>
                    {#snippet children({ props })}
                        <Form.Label
                            >Password
                            <Muted class="float-right">
                                <A href="/forgot-password">Forgot password?</A>
                            </Muted></Form.Label
                        >
                        <Input
                            {...props}
                            bind:value={$formData.password}
                            type="password"
                            placeholder="Enter password"
                            autocomplete="current-password"
                            maxlength={100}
                            minlength={6}
                            required
                        />
                    {/snippet}
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
                    <Button aria-label="Login with Microsoft" onclick={() => liveLogin(redirectUrl)}>
                        <MicrosoftIcon class="size-4" /> Microsoft
                    </Button>
                {/if}
                {#if googleClientId}
                    <Button aria-label="Login with Google" onclick={() => googleLogin(redirectUrl)}>
                        <GoogleIcon class="size-4" /> Google
                    </Button>
                {/if}
                {#if facebookClientId}
                    <Button aria-label="Login with Facebook" onclick={() => facebookLogin(redirectUrl)}>
                        <Facebook class="size-4" /> Facebook
                    </Button>
                {/if}
                {#if gitHubClientId}
                    <Button aria-label="Login with GitHub" onclick={() => githubLogin(redirectUrl)}>
                        <GitHub class="size-4" /> GitHub
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
    </Card.Content>
</Card.Root>
