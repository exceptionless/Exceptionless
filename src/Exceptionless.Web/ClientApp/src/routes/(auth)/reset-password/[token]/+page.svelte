<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import Logo from '$comp/logo.svelte';
    import { A, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { cancelResetPassword, resetPassword } from '$features/auth/index.svelte';
    import { type ResetPasswordFormData, ResetPasswordSchema } from '$features/auth/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    const token = page.params.token;
    const shouldCancel = page.url.searchParams.get('cancel') === 'true';

    const form = createForm(() => ({
        defaultValues: {
            confirm_password: '',
            password: '',
            password_reset_token: token
        } as ResetPasswordFormData,
        validators: {
            onSubmit: ResetPasswordSchema,
            onSubmitAsync: async ({ value }) => {
                const response = await resetPassword(value.password_reset_token, value.password);
                if (response.ok) {
                    toast.success('You have successfully changed your password.');
                    await goto(resolve('/(auth)/login'));
                    return null;
                }

                if (response.status === 422 && response.problem?.errors?.password_reset_token) {
                    toast.error('The password reset token is invalid or has expired. Please request a new password reset.');
                    await goto(resolve('/(auth)/forgot-password'));
                    return null;
                }

                toast.error('An error occurred while trying to change your password.');
                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));

    async function handleCancelToken() {
        if (shouldCancel && token) {
            await cancelResetPassword(token);
            await goto(resolve('/(auth)/login'));
        }
    }

    $effect(() => {
        handleCancelToken();
    });
</script>

{#if shouldCancel}
    <Card.Root class="mx-auto w-sm">
        <Card.Header>
            <Logo />
            <Card.Title class="text-center text-2xl">Cancelling password reset...</Card.Title>
        </Card.Header>
        <Card.Content>
            <div class="flex justify-center">
                <Spinner class="size-8" />
            </div>
        </Card.Content>
    </Card.Root>
{:else}
    <Card.Root class="mx-auto w-sm">
        <Card.Header>
            <Logo />
            <Card.Title class="text-center text-2xl">Change password</Card.Title>
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
                <form.Field name="password">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>New Password</Field.Label>
                            <Input
                                id={field.name}
                                name={field.name}
                                type="password"
                                placeholder="New Password"
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
                <form.Field name="confirm_password">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Confirm Password</Field.Label>
                            <Input
                                id={field.name}
                                name={field.name}
                                type="password"
                                placeholder="Confirm Password"
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
                                <Spinner /> Changing Password...
                            {:else}
                                Change Password
                            {/if}
                        </Button>
                    {/snippet}
                </form.Subscribe>
            </form>

            <P class="mt-4 text-center text-sm">
                <A href={resolve('/(auth)/login')}>Back to Login</A>
            </P>
        </Card.Content>
    </Card.Root>
{/if}
