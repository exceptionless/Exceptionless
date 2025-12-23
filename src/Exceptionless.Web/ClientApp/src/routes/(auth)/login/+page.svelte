<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import GoogleIcon from '$comp/icons/GoogleIcon.svelte';
    import MicrosoftIcon from '$comp/icons/MicrosoftIcon.svelte';
    import Logo from '$comp/logo.svelte';
    import { A, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
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
    import { type LoginFormData, loginSchema } from '$features/auth/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import Facebook from '@lucide/svelte/icons/facebook';
    import GitHub from '@lucide/svelte/icons/github';
    import { createForm } from '@tanstack/svelte-form';

    const redirectUrl = page.url.searchParams.get('redirect') ?? resolve('/(app)');

    const form = createForm(() => ({
        defaultValues: {
            email: '',
            invite_token: page.url.searchParams.get('token'),
            password: ''
        } as LoginFormData,
        validators: {
            onSubmit: loginSchema,
            onSubmitAsync: async ({ value }) => {
                const response = await login(value.email, value.password);
                if (response.ok) {
                    await goto(redirectUrl);
                    return null;
                }

                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));
</script>

<Card.Root class="mx-auto max-w-sm">
    <Card.Header>
        <Logo />
        <Card.Title class="text-center text-2xl">Log in to your account</Card.Title>
    </Card.Header>
    <Card.Content>
        <form
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                form.handleSubmit();
            }}
        >
            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>
            <form.Field name="email">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Email</Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            type="email"
                            placeholder="Enter email address"
                            autocomplete="email"
                            required
                            value={field.state.value}
                            onblur={field.handleBlur}
                            oninput={(e) => field.handleChange(e.currentTarget.value)}
                            aria-invalid={ariaInvalid(field)}
                        />
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </form.Field>
            <form.Field name="password">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}
                            >Password
                            <Muted class="float-right">
                                <A href={resolve('/(auth)/forgot-password')}>Forgot password?</A>
                            </Muted></Field.Label
                        >
                        <Input
                            id={field.name}
                            name={field.name}
                            type="password"
                            placeholder="Enter password"
                            autocomplete="current-password"
                            maxlength={100}
                            minlength={6}
                            required
                            value={field.state.value}
                            onblur={field.handleBlur}
                            oninput={(e) => field.handleChange(e.currentTarget.value)}
                            aria-invalid={ariaInvalid(field)}
                        />
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </form.Field>
            <form.Subscribe selector={(state) => state.isSubmitting}>
                {#snippet children(isSubmitting)}
                    <Button type="submit" class="mt-4 w-full" disabled={isSubmitting}>
                        {#if isSubmitting}
                            <Spinner /> Logging in...
                        {:else}
                            Login
                        {/if}
                    </Button>
                {/snippet}
            </form.Subscribe>
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
                <A href={resolve('/(auth)/signup')}>Start a free trial</A>
            </P>

            <P class="text-center text-sm">
                By signing up, you agree to our <A href="https://exceptionless.com/privacy" target="_blank">Privacy Policy</A>
                and
                <A href="https://exceptionless.com/terms" target="_blank">Terms of Service</A>.
            </P>
        {/if}
    </Card.Content>
</Card.Root>
