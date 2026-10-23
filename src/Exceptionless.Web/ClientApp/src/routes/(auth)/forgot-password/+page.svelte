<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    import ErrorMessage from '#comp/error-message.svelte';
    import Logo from '#comp/logo.svelte';
    import { A, P } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { forgotPassword } from '#features/auth/api.svelte.ts';
    import { type ForgotPasswordFormData, ForgotPasswordSchema } from '#features/auth/schemas.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#shared/validation.ts';

    const form = createForm(() => ({
        defaultValues: {
            email: ''
        } as ForgotPasswordFormData,
        validators: {
            onSubmit: ForgotPasswordSchema,
            onSubmitAsync: async ({ value }) => {
                const response = await forgotPassword(value.email);
                if (response.ok) {
                    toast.success('Please check your inbox for the password reset email.');
                    await goto(resolve('/(auth)/login'));
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
        <Card.Title class="text-center text-2xl">Reset your password</Card.Title>
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
                            placeholder="Email Address"
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
            <form.Subscribe selector={(state) => state.isSubmitting}>
                {#snippet children(isSubmitting)}
                    <Button type="submit" class="mt-4 w-full" disabled={isSubmitting}>
                        {#if isSubmitting}
                            <Spinner /> Sending...
                        {:else}
                            Send Reset Email
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
