<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import { A, Muted, Small } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as InputGroup from '$comp/ui/input-group';
    import { Spinner } from '$comp/ui/spinner';
    import { logout } from '$features/auth/api.svelte';
    import { validateEmailAvailability } from '$features/auth/validators';
    import { deleteCurrentUser, getMeQuery, patchUser, postEmailAddress, resendVerificationEmail } from '$features/users/api.svelte';
    import DeleteCurrentUserDialog from '$features/users/components/dialogs/delete-current-user-dialog.svelte';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import { type UpdateUserEmailAddressFormData, UpdateUserEmailAddressSchema, type UpdateUserFormData, UpdateUserSchema } from '$features/users/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
    import Trash from '@lucide/svelte/icons/trash-2';
    import { createForm } from '@tanstack/svelte-form';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();
    let showDeleteAccountDialog = $state(false);
    const client = useFetchClient();
    const queryClient = useQueryClient();
    const meQuery = getMeQuery();
    const gravatar = getGravatarFromCurrentUser(meQuery);
    const deleteAccountMutation = deleteCurrentUser();
    const updateUser = patchUser({
        route: {
            get id() {
                return meQuery.data?.id;
            }
        }
    });

    const isEmailAddressVerified = $derived(meQuery.data?.is_email_address_verified ?? false);
    const resendVerificationEmailMutation = resendVerificationEmail({
        route: {
            get id() {
                return meQuery.data?.id;
            }
        }
    });

    const updateEmailAddress = postEmailAddress({
        route: {
            get id() {
                return meQuery.data?.id;
            }
        }
    });

    const updateEmailAddressForm = createForm(() => ({
        defaultValues: {
            email_address: meQuery.data?.email_address
        } as UpdateUserEmailAddressFormData,
        validators: {
            onSubmit: UpdateUserEmailAddressSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    await updateEmailAddress.mutateAsync(value as UpdateUserEmailAddressFormData);
                    toastId = toast.success('Successfully updated Account');
                    return null;
                } catch (error: unknown) {
                    toastId = toast.error('Error saving email address. Please try again.');
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred, please try again.' };
                }
            }
        }
    }));

    const updateUserForm = createForm(() => ({
        defaultValues: {
            full_name: meQuery.data?.full_name
        } as UpdateUserFormData,
        validators: {
            onSubmit: UpdateUserSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    await updateUser.mutateAsync(value as UpdateUserFormData);
                    toastId = toast.success('Successfully updated Account');
                    return null;
                } catch (error: unknown) {
                    toastId = toast.error('Error saving full name. Please try again.');
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred, please try again.' };
                }
            }
        }
    }));

    const debouncedUpdateEmailAddressFormSubmit = debounce(1000, () => updateEmailAddressForm.handleSubmit());
    const debouncedUpdatedUserFormSubmit = debounce(1000, () => updateUserForm.handleSubmit());

    async function handleResendVerificationEmail() {
        toast.dismiss(toastId);
        try {
            await resendVerificationEmailMutation.mutateAsync();
            toastId = toast.success('Please check your inbox for the verification email.');
        } catch {
            toastId = toast.error('Error sending verification email. Please try again.');
        }
    }

    async function deleteAccount() {
        toast.dismiss(toastId);
        try {
            await deleteAccountMutation.mutateAsync();
            toastId = toast.success('Successfully queued your account for deletion.');
            await logout(queryClient, client);
            await goto(resolve('/(auth)/login'), { replaceState: true });
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to delete your account: ${message}`);
        }
    }
</script>

<div class="space-y-6">
    <Avatar.Root class="h-24 w-24" title="Profile Image">
        {#await gravatar.src}
            <Avatar.Fallback>{gravatar.initials}</Avatar.Fallback>
        {:then src}
            <Avatar.Image alt={meQuery.data ? `${meQuery.data.full_name} avatar` : 'avatar'} {src} />
        {/await}
        <Avatar.Fallback>{gravatar.initials}</Avatar.Fallback>
    </Avatar.Root>
    <Muted>Your avatar is generated by requesting a Gravatar image with the email address below.</Muted>

    <div class="space-y-4">
        <form
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                updateUserForm.handleSubmit();
            }}
        >
            <updateUserForm.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </updateUserForm.Subscribe>
            <updateUserForm.Field name="full_name">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Full Name</Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            placeholder="Full Name"
                            autocomplete="name"
                            required
                            value={field.state.value}
                            onblur={field.handleBlur}
                            oninput={(e) => {
                                field.handleChange(e.currentTarget.value);
                                debouncedUpdatedUserFormSubmit();
                            }}
                            aria-invalid={ariaInvalid(field)}
                        />
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </updateUserForm.Field>
        </form>
        <form
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                updateEmailAddressForm.handleSubmit();
            }}
        >
            <updateEmailAddressForm.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </updateEmailAddressForm.Subscribe>
            <updateEmailAddressForm.Field
                name="email_address"
                validators={{ onChangeAsync: ({ value }) => validateEmailAvailability(value ?? ''), onChangeAsyncDebounceMs: 1000 }}
            >
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Email</Field.Label>
                        <InputGroup.Root>
                            <InputGroup.Input
                                id={field.name}
                                name={field.name}
                                type="email"
                                placeholder="Enter email address"
                                autocomplete="email"
                                required
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => {
                                    field.handleChange(e.currentTarget.value);
                                    debouncedUpdateEmailAddressFormSubmit();
                                }}
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
            </updateEmailAddressForm.Field>
        </form>
    </div>
    {#if !isEmailAddressVerified}
        <Small>
            Email not verified. <A class="cursor-pointer" onclick={handleResendVerificationEmail}>Resend</A> verification email.
        </Small>
    {/if}

    <div class="border-t pt-6">
        <Button variant="destructive" onclick={() => (showDeleteAccountDialog = true)} disabled={deleteAccountMutation.isPending}>
            {#if deleteAccountMutation.isPending}
                <Spinner class="mr-2 size-4" />
                Deleting Account...
            {:else}
                <Trash class="mr-2 size-4" />
                Delete Account
            {/if}
        </Button>
    </div>
</div>

<DeleteCurrentUserDialog bind:open={showDeleteAccountDialog} remove={deleteAccount} />
