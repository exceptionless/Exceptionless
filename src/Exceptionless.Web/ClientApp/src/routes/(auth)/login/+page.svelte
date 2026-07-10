<script lang="ts">
    import { dev } from '$app/environment';
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import FacebookIcon from '$comp/icons/FacebookIcon.svelte';
    import GitHubIcon from '$comp/icons/GitHubIcon.svelte';
    import GoogleIcon from '$comp/icons/GoogleIcon.svelte';
    import MicrosoftIcon from '$comp/icons/MicrosoftIcon.svelte';
    import Logo from '$comp/logo.svelte';
    import { A, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { login } from '$features/auth/api.svelte';
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
        microsoftClientId
    } from '$features/auth/index.svelte';
    import { type LoginFormData, LoginSchema } from '$features/auth/schemas';
    import { getSafeRedirectUrl } from '$features/shared/url';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { createForm } from '@tanstack/svelte-form';

    const defaultRedirect = resolve('/(app)/stack');
    const redirectUrl = getSafeRedirectUrl(page.url.searchParams.get('redirect'), defaultRedirect);

    const form = createForm(() => ({
        defaultValues: {
            email: '',
            invite_token: page.url.searchParams.get('token'),
            password: ''
        } as LoginFormData,
        validators: {
            onSubmit: LoginSchema,
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

    function prefillDevCredentials(): void {
        form.setFieldValue('email', 'admin@exceptionless.test');
        form.setFieldValue('password', 'tester');
    }
</script>

<div class="mx-auto flex w-[calc(100vw-2rem)] max-w-lg flex-col items-center">
    <Card.Root class="w-full">
        <Card.Header>
            <Logo />
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
                {#if dev}
                    <div class="mb-2 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-center text-xs">
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
                                id={field.name}
                                name={field.name}
                                type={dev ? 'text' : 'email'}
                                placeholder="Enter email address"
                                autocomplete="email"
                                required
                                tabindex={1}
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
                                >Password <Muted class="float-right"><A href={resolve('/(auth)/forgot-password')} tabindex={6}>Forgot password?</A></Muted
                                ></Field.Label
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
                                tabindex={2}
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
                        <div class={enableAccountCreation ? 'mt-4 grid grid-cols-2 gap-3' : 'mt-4'}>
                            <Button type="submit" class="w-full" tabindex={3} disabled={isSubmitting}>
                                {#if isSubmitting}
                                    <Spinner /> Logging in...
                                {:else}
                                    Login
                                {/if}
                            </Button>
                            {#if enableAccountCreation}
                                <Button variant="secondary" href={resolve('/(auth)/signup')} class="w-full" tabindex={4}>Signup</Button>
                            {/if}
                        </div>
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
                        <Button aria-label="Login with Microsoft" tabindex={4} onclick={() => liveLogin(redirectUrl)}>
                            <MicrosoftIcon class="size-4" /> Microsoft
                        </Button>
                    {/if}
                    {#if googleClientId}
                        <Button aria-label="Login with Google" tabindex={4} onclick={() => googleLogin(redirectUrl)}>
                            <GoogleIcon class="size-4" /> Google
                        </Button>
                    {/if}
                    {#if facebookClientId}
                        <Button aria-label="Login with Facebook" tabindex={4} onclick={() => facebookLogin(redirectUrl)}>
                            <FacebookIcon class="size-4" /> Facebook
                        </Button>
                    {/if}
                    {#if gitHubClientId}
                        <Button aria-label="Login with GitHub" tabindex={4} onclick={() => githubLogin(redirectUrl)}>
                            <GitHubIcon class="size-4" /> GitHub
                        </Button>
                    {/if}
                </div>
            {/if}

            {#if enableAccountCreation}
                <P class="text-center text-sm">
                    Not a member?
                    <A href={resolve('/(auth)/signup')} tabindex={5}>Start a free trial</A>
                </P>
            {/if}
        </Card.Content>
    </Card.Root>

    {#if enableAccountCreation}
        <P class="text-muted-foreground mt-3! px-4 text-center text-sm">
            By signing up, you agree to our <A href="https://exceptionless.com/privacy" target="_blank">Privacy Policy</A>
            and
            <A href="https://exceptionless.com/terms" target="_blank">Terms of Service</A>.
        </P>
    {/if}
</div>
