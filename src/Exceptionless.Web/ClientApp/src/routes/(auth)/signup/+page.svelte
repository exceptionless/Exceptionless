<script lang="ts">
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
    import * as InputGroup from '#comp/ui/input-group/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { signup } from '#features/auth/api.svelte.ts';
    import OAuthProviderButtons from '#features/auth/components/oauth-provider-buttons.svelte';
    import PasswordInput from '#features/auth/components/password-input.svelte';
    import SignedInNotice from '#features/auth/components/signed-in-notice.svelte';
    import { accessToken, enableOAuthLogin, validateEmailAvailability } from '#features/auth/index.svelte.ts';
    import { buildAuthPageHref, getAuthPageParams, getPostLoginRedirect, getPostSignupRedirect } from '#features/auth/redirects.ts';
    import { type SignupFormData, SignupSchema } from '#features/auth/schemas.ts';
    import { isStripeEnabled } from '#features/billing/stripe.svelte.ts';
    import { gotoRedirect } from '#features/navigation/redirect.ts';
    import ExieGuide from '#features/onboarding/components/exie-guide.svelte';
    import OnboardingStepper from '#features/onboarding/components/onboarding-stepper.svelte';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors, submitWhenValidated } from '#shared/validation.ts';

    const params = getAuthPageParams(page.url.searchParams);
    const inviteToken = params.token;
    const loginHref = buildAuthPageHref(resolve('/(auth)/login'), params);
    // Hosted signups start a no-card trial of every feature; self-hosted installs and invited users don't.
    const signupTagline = isStripeEnabled() && !inviteToken ? 'Try every feature free · No credit card required' : 'Free to start · No credit card required';

    // Only a session that existed when the page opened is "already signed in"; a successful signup also sets one.
    const hadSessionOnLoad = !!accessToken.current;
    const isSignedIn = $derived(hadSessionOnLoad && !!accessToken.current);
    const isLeaving = $derived(isSignedIn && !inviteToken);

    onMount(() => {
        if (isLeaving) {
            void gotoRedirect(getPostLoginRedirect(params), {
                replace: true
            });
        }
    });

    const form = createForm(() => ({
        defaultValues: {
            email: '',
            invite_token: inviteToken,
            name: '',
            password: ''
        } as SignupFormData,
        validators: {
            onSubmit: SignupSchema,
            onSubmitAsync: async ({ value }) => {
                const response = await signup(value.name.trim(), value.email.trim(), value.password, value.invite_token);
                if (response.ok) {
                    await gotoRedirect(getPostSignupRedirect(params, 'password'));
                    return null;
                }

                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));
</script>

<div class="mx-auto grid w-full max-w-5xl items-center gap-6 md:grid-cols-[minmax(0,1fr)_minmax(0,28rem)] md:gap-12 md:self-center">
    <section aria-label="Getting started with Exie" class="hidden flex-col gap-8 md:flex">
        <Logo class="mx-0 h-9 max-h-none w-auto self-start object-contain" />
        <ExieGuide live={false} mood="hello" size={120} title="Hi, I'm Exie!">I'll help you catch your first error in about 5 minutes.</ExieGuide>
        <div class="flex flex-col gap-3">
            <p class="text-muted-foreground text-xs font-semibold tracking-wide uppercase">Here's the plan</p>
            <OnboardingStepper orientation="vertical" showDescriptions />
        </div>
    </section>

    <div class="flex w-full flex-col gap-4">
        <div class="flex flex-col items-center gap-4 md:hidden">
            <Logo class="h-8 max-h-none w-auto object-contain" />
            <ExieGuide class="w-full" live={false} mood="hello" size={48}>Hi, I'm Exie! Let's catch your first error in about 5 minutes.</ExieGuide>
        </div>

        {#if isLeaving}
            <Card.Root class="w-full">
                <Card.Content class="flex items-center gap-3">
                    <Spinner /> You're already signed in. Taking you to Exceptionless…
                </Card.Content>
            </Card.Root>
        {:else if isSignedIn}
            <SignedInNotice continueHref={getPostLoginRedirect(params)} />
        {:else}
            <Card.Root class="w-full">
                <Card.Header>
                    <h1 class="text-foreground text-2xl font-semibold tracking-tight">Create your account</h1>
                    <Card.Description>{signupTagline}</Card.Description>
                </Card.Header>
                <Card.Content class="flex flex-col gap-4">
                    {#if enableOAuthLogin}
                        <OAuthProviderButtons {inviteToken} mode="signup" redirectUrl={getPostSignupRedirect(params, 'oauth')} />
                        <Field.Separator class="*:data-[slot=field-separator-content]:bg-card">or use your email</Field.Separator>
                    {/if}

                    <form
                        novalidate
                        onsubmit={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            void submitWhenValidated(form);
                        }}
                    >
                        <Field.Group>
                            <form.Subscribe selector={(state) => state.errors}>
                                {#snippet children(errors)}
                                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                                {/snippet}
                            </form.Subscribe>
                            <form.Field name="name">
                                {#snippet children(field)}
                                    <Field.Field data-invalid={ariaInvalid(field)}>
                                        <Field.Label for={field.name}>Name</Field.Label>
                                        <Input
                                            aria-invalid={ariaInvalid(field)}
                                            autocapitalize="words"
                                            autocomplete="name"
                                            autofocus
                                            id={field.name}
                                            name={field.name}
                                            onblur={field.handleBlur}
                                            oninput={(e) => field.handleChange(e.currentTarget.value)}
                                            placeholder="Your first and last name"
                                            required
                                            type="text"
                                            value={field.state.value}
                                        />
                                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                    </Field.Field>
                                {/snippet}
                            </form.Field>
                            <form.Field
                                name="email"
                                validators={{
                                    onChangeAsync: ({ value }) => validateEmailAvailability(value),
                                    onChangeAsyncDebounceMs: 1000
                                }}
                            >
                                {#snippet children(field)}
                                    <Field.Field data-invalid={ariaInvalid(field)}>
                                        <Field.Label for={field.name}>Email</Field.Label>
                                        <InputGroup.Root>
                                            <InputGroup.Input
                                                aria-invalid={ariaInvalid(field)}
                                                autocomplete="email"
                                                id={field.name}
                                                name={field.name}
                                                onblur={field.handleBlur}
                                                oninput={(e) => field.handleChange(e.currentTarget.value)}
                                                placeholder="you@company.com"
                                                required
                                                type="email"
                                                value={field.state.value}
                                            />
                                            {#if field.state.meta.isValidating}
                                                <InputGroup.Addon align="inline-end" aria-label="Validating email">
                                                    <Spinner class="size-4" />
                                                </InputGroup.Addon>
                                            {/if}
                                        </InputGroup.Root>
                                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                    </Field.Field>
                                {/snippet}
                            </form.Field>
                            <form.Field name="password">
                                {#snippet children(field)}
                                    <Field.Field data-invalid={ariaInvalid(field)}>
                                        <Field.Label for={field.name}>Password</Field.Label>
                                        <PasswordInput
                                            aria-describedby={field.state.meta.errors.length > 0 ? undefined : 'password-hint'}
                                            aria-invalid={ariaInvalid(field)}
                                            autocomplete="new-password"
                                            id={field.name}
                                            maxlength={100}
                                            minlength={6}
                                            name={field.name}
                                            onblur={field.handleBlur}
                                            oninput={(e) => field.handleChange(e.currentTarget.value)}
                                            required
                                            value={field.state.value}
                                        />
                                        {#if field.state.meta.errors.length > 0}
                                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                        {:else}
                                            <Field.Description id="password-hint">At least 6 characters</Field.Description>
                                        {/if}
                                    </Field.Field>
                                {/snippet}
                            </form.Field>
                            <form.Subscribe selector={(state) => state.isSubmitting}>
                                {#snippet children(isSubmitting)}
                                    <Button class="w-full" disabled={isSubmitting} size="lg" type="submit">
                                        {#if isSubmitting}
                                            <Spinner data-icon="inline-start" /> Creating your account…
                                        {:else}
                                            Create account
                                        {/if}
                                    </Button>
                                {/snippet}
                            </form.Subscribe>
                        </Field.Group>
                    </form>
                </Card.Content>
                <Card.Footer class="flex-col gap-2 text-center text-sm">
                    <p>Already have an account? <A href={loginHref}>Log in</A></p>
                    <Muted class="text-xs">
                        By signing up, you agree to our <A href="https://exceptionless.com/privacy" target="_blank">Privacy Policy</A> and
                        <A href="https://exceptionless.com/terms" target="_blank">Terms of Service</A>.
                    </Muted>
                </Card.Footer>
            </Card.Root>
        {/if}
    </div>
</div>
