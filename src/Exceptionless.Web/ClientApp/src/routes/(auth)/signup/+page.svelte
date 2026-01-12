<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import GoogleIcon from '$comp/icons/GoogleIcon.svelte';
    import MicrosoftIcon from '$comp/icons/MicrosoftIcon.svelte';
    import Logo from '$comp/logo.svelte';
    import { A, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as InputGroup from '$comp/ui/input-group';
    import { Spinner } from '$comp/ui/spinner';
    import {
        enableOAuthLogin,
        facebookClientId,
        facebookLogin,
        gitHubClientId,
        githubLogin,
        googleClientId,
        googleLogin,
        liveLogin,
        microsoftClientId,
        signup
    } from '$features/auth/index.svelte';
    import { type SignupFormData, SignupSchema } from '$features/auth/schemas';
    import { validateEmailAvailability } from '$features/auth/validators';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import Facebook from '@lucide/svelte/icons/facebook';
    import GitHub from '@lucide/svelte/icons/github';
    import { createForm } from '@tanstack/svelte-form';

    const redirectUrl = resolve('/(app)/project/add');
    const inviteToken = page.url.searchParams.get('token');

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
                const response = await signup(value.name, value.email, value.password, value.invite_token);
                if (response.ok) {
                    await goto(redirectUrl);
                    return null;
                }

                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));
</script>

<Card.Root class="mx-auto w-sm">
    <Card.Header>
        <Logo />
        <Card.Title class="text-center text-2xl">Signup for a FREE account in seconds</Card.Title>
    </Card.Header>
    <Card.Content>
        {#if enableOAuthLogin}
            <P class="text-center">Sign up with</P>
            <div class="auto-cols-2 grid grid-flow-col grid-rows-2 gap-4">
                {#if microsoftClientId}
                    <Button aria-label="Sign up with Microsoft" onclick={() => liveLogin(redirectUrl)}>
                        <MicrosoftIcon class="size-4" /> Microsoft
                    </Button>
                {/if}
                {#if googleClientId}
                    <Button aria-label="Sign up with Google" onclick={() => googleLogin(redirectUrl)}>
                        <GoogleIcon class="size-4" /> Google
                    </Button>
                {/if}
                {#if facebookClientId}
                    <Button aria-label="Sign up with Facebook" onclick={() => facebookLogin(redirectUrl)}>
                        <Facebook class="size-4" /> Facebook
                    </Button>
                {/if}
                {#if gitHubClientId}
                    <Button aria-label="Sign up with GitHub" onclick={() => githubLogin(redirectUrl)}>
                        <GitHub class="size-4" /> GitHub
                    </Button>
                {/if}
            </div>

            <div class="my-4 flex w-full items-center">
                <hr class="w-full" />
                <P class="px-3">OR</P>
                <hr class="w-full" />
            </div>
        {/if}

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
            <form.Field name="name">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Name</Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            type="text"
                            placeholder="Your first and last name"
                            autocomplete="name"
                            autocapitalize="words"
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
            <form.Field name="email" validators={{ onChangeAsync: ({ value }) => validateEmailAvailability(value), onChangeAsyncDebounceMs: 1000 }}>
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Email</Field.Label>
                        <InputGroup.Root>
                            <InputGroup.Input
                                id={field.name}
                                name={field.name}
                                type="email"
                                placeholder="Email address (no spam)"
                                autocomplete="email"
                                required
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(e.currentTarget.value)}
                                aria-invalid={ariaInvalid(field)}
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
                        <Input
                            id={field.name}
                            name={field.name}
                            type="password"
                            placeholder="Password"
                            autocomplete="new-password"
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
                            <Spinner /> Creating Account...
                        {:else}
                            Create My Account
                        {/if}
                    </Button>
                {/snippet}
            </form.Subscribe>
        </form>

        <P class="mt-4 text-center text-sm">
            Already have an account?
            <A href={inviteToken ? `${resolve('/(auth)/login')}?token=${inviteToken}` : resolve('/(auth)/login')}>Log In</A>
        </P>

        <P class="text-center text-sm">
            By signing up, you agree to our <A href="https://exceptionless.com/privacy" target="_blank">Privacy Policy</A>
            and
            <A href="https://exceptionless.com/terms" target="_blank">Terms of Service</A>.
        </P>
    </Card.Content>
</Card.Root>
