<script lang="ts">
    import { dev } from '$app/env';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { createForm } from '@tanstack/svelte-form';
    import { onMount } from 'svelte';

    import ErrorMessage from '#comp/error-message.svelte';
    import Logo from '#comp/logo.svelte';
    import { A, Muted } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { login } from '#features/auth/api.svelte.ts';
    import OAuthProviderButtons from '#features/auth/components/oauth-provider-buttons.svelte';
    import PasswordInput from '#features/auth/components/password-input.svelte';
    import SignedInNotice from '#features/auth/components/signed-in-notice.svelte';
    import { accessToken, enableAccountCreation, enableOAuthLogin } from '#features/auth/index.svelte.ts';
    import { buildAuthPageHref, getAuthPageParams, getPostLoginRedirect } from '#features/auth/redirects.ts';
    import { type LoginFormData, LoginSchema } from '#features/auth/schemas.ts';
    import { gotoRedirect } from '#features/navigation/redirect.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#shared/validation.ts';

    const params = getAuthPageParams(page.url.searchParams);
    const inviteToken = params.token;
    const redirectUrl = getPostLoginRedirect(params);
    const canSignup = enableAccountCreation || !!inviteToken;
    const signupHref = buildAuthPageHref(resolve('/(auth)/signup'), params);

    // Only a session that existed when the page opened is "already signed in"; a successful login also sets one.
    const hadSessionOnLoad = !!accessToken.current;
    const isSignedIn = $derived(hadSessionOnLoad && !!accessToken.current);
    const isLeaving = $derived(isSignedIn && !inviteToken);

    onMount(() => {
        if (isLeaving) {
            void gotoRedirect(redirectUrl, {
                replace: true
            });
        }
    });

    const form = createForm(() => ({
        defaultValues: {
            email: '',
            invite_token: inviteToken,
            password: ''
        } as LoginFormData,
        validators: {
            onSubmit: LoginSchema,
            onSubmitAsync: async ({ value }) => {
                const response = await login(value.email.trim(), value.password, value.invite_token);
                if (response.ok) {
                    await gotoRedirect(redirectUrl);
                    return null;
                }

                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));

    function prefillDevCredentials(): void {
        form.setFieldValue('email', 'admin@exceptionless.test');
        form.setFieldValue('password', 'tester');
    }
</script>

<div class="mx-auto flex w-full max-w-md flex-col items-center gap-4 md:self-center">
    <Logo class="h-9 max-h-none w-auto object-contain" />

    {#if isLeaving}
        <Card.Root class="w-full">
            <Card.Content class="flex items-center gap-3">
                <Spinner /> You're already signed in. Taking you to Exceptionless…
            </Card.Content>
        </Card.Root>
    {:else if isSignedIn}
        <SignedInNotice continueHref={redirectUrl} />
    {:else}
        <Card.Root class="w-full">
            <Card.Header>
                <h1 class="text-foreground text-2xl font-semibold tracking-tight">Welcome back</h1>
                <Card.Description>Log in to see what your apps have been up to.</Card.Description>
            </Card.Header>
            <Card.Content class="flex flex-col gap-4">
                <form
                    novalidate
                    onsubmit={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        form.handleSubmit();
                    }}
                >
                    <Field.Group>
                        <form.Subscribe selector={(state) => state.errors}>
                            {#snippet children(errors)}
                                <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                            {/snippet}
                        </form.Subscribe>
                        {#if dev}
                            <div class="-mb-2 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-center text-xs">
                                <Muted class="block">
                                    Default credentials: <strong>admin@exceptionless.test</strong> / <strong>tester</strong>
                                </Muted>
                                <Button type="button" variant="link" size="xs" class="h-auto px-0 py-0 text-xs" onclick={prefillDevCredentials}>Use</Button>
                            </div>
                        {/if}
                        <form.Field name="email">
                            {#snippet children(field)}
                                <Field.Field data-invalid={ariaInvalid(field)}>
                                    <Field.Label for={field.name}>Email</Field.Label>
                                    <Input
                                        aria-invalid={ariaInvalid(field)}
                                        autocomplete="email"
                                        id={field.name}
                                        name={field.name}
                                        onblur={field.handleBlur}
                                        oninput={(e) => field.handleChange(e.currentTarget.value)}
                                        placeholder="you@company.com"
                                        required
                                        type={dev ? 'text' : 'email'}
                                        value={field.state.value}
                                    />
                                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                </Field.Field>
                            {/snippet}
                        </form.Field>
                        <form.Field name="password">
                            {#snippet children(field)}
                                <Field.Field data-invalid={ariaInvalid(field)}>
                                    <div class="flex items-center justify-between gap-2">
                                        <Field.Label for={field.name}>Password</Field.Label>
                                        <A class="text-sm" href={resolve('/(auth)/forgot-password')}>Forgot password?</A>
                                    </div>
                                    <PasswordInput
                                        aria-invalid={ariaInvalid(field)}
                                        autocomplete="current-password"
                                        id={field.name}
                                        maxlength={100}
                                        minlength={6}
                                        name={field.name}
                                        onblur={field.handleBlur}
                                        oninput={(e) => field.handleChange(e.currentTarget.value)}
                                        placeholder="Enter password"
                                        required
                                        value={field.state.value}
                                    />
                                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                </Field.Field>
                            {/snippet}
                        </form.Field>
                        <form.Subscribe selector={(state) => state.isSubmitting}>
                            {#snippet children(isSubmitting)}
                                <Button class="w-full" disabled={isSubmitting} size="lg" type="submit">
                                    {#if isSubmitting}
                                        <Spinner data-icon="inline-start" /> Logging in…
                                    {:else}
                                        Login
                                    {/if}
                                </Button>
                            {/snippet}
                        </form.Subscribe>
                    </Field.Group>
                </form>

                {#if enableOAuthLogin}
                    <Field.Separator class="*:data-[slot=field-separator-content]:bg-card">or</Field.Separator>
                    <OAuthProviderButtons {inviteToken} mode="login" {redirectUrl} />
                {/if}
            </Card.Content>
            {#if canSignup}
                <Card.Footer class="justify-center text-sm">
                    <p>New to Exceptionless? <A href={signupHref}>Create an account</A></p>
                </Card.Footer>
            {/if}
        </Card.Root>

        {#if canSignup}
            <Muted class="px-4 text-center text-xs">
                By continuing, you agree to our <A href="https://exceptionless.com/privacy" target="_blank">Privacy Policy</A> and
                <A href="https://exceptionless.com/terms" target="_blank">Terms of Service</A>.
            </Muted>
        {/if}
    {/if}
</div>
