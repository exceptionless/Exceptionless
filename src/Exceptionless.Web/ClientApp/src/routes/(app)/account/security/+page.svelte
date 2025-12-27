<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { changePassword } from '$features/auth/index.svelte';
    import { ChangePasswordSchema, ChangePasswordWithCurrentSchema } from '$features/auth/schemas';
    import { getMeQuery } from '$features/users/api.svelte';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    let toastId = $state<number | string>();
    const meQuery = getMeQuery();

    const hasLocalAccount = $derived(meQuery.data?.has_local_account ?? false);

    const form = createForm(() => ({
        defaultValues: {
            confirm_password: '',
            current_password: '',
            password: ''
        },
        validators: {
            onSubmit: hasLocalAccount ? ChangePasswordWithCurrentSchema : ChangePasswordSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);

                const response = await changePassword(hasLocalAccount ? value.current_password : undefined, value.password);
                if (response.ok) {
                    await meQuery.refetch();
                    form.reset();
                    toastId = toast.success(hasLocalAccount ? 'Password changed successfully.' : 'Password set successfully.');
                    return null;
                }

                toastId = toast.error(hasLocalAccount ? 'Error changing password. Please try again.' : 'Error setting password. Please try again.');
                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));
</script>

<div class="space-y-6">
    <div>
        <H3>Change Password</H3>
        <Muted>{hasLocalAccount ? 'Change your password.' : 'Set a password to enable password-based sign in.'}</Muted>
    </div>
    <Separator />

    <form
        class="max-w-md space-y-4"
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

        {#if hasLocalAccount}
            <form.Field name="current_password">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Current Password</Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            type="password"
                            placeholder="Enter current password"
                            autocomplete="current-password"
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
        {/if}

        <form.Field name="password">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>{hasLocalAccount ? 'New Password' : 'Password'}</Field.Label>
                    <Input
                        id={field.name}
                        name={field.name}
                        type="password"
                        placeholder={hasLocalAccount ? 'Enter new password' : 'Enter password'}
                        autocomplete="new-password"
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
                        placeholder="Confirm password"
                        autocomplete="new-password"
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
                <Button type="submit" disabled={isSubmitting}>
                    {#if isSubmitting}
                        {hasLocalAccount ? 'Changing Password...' : 'Setting Password...'}
                    {:else}
                        {hasLocalAccount ? 'Change Password' : 'Set Password'}
                    {/if}
                </Button>
            {/snippet}
        </form.Subscribe>
    </form>
</div>
