<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { A, H3, Muted, Small } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { getMeQuery, patchUser, postEmailAddress, resendVerificationEmail } from '$features/users/api.svelte';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import { UpdateUser, UpdateUserEmailAddress } from '$features/users/models';
    import { applyServerSideErrors } from '$shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();
    const meQuery = getMeQuery();
    const gravatar = getGravatarFromCurrentUser(meQuery);
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

    const updateEmailAddressForm = superForm(defaults(meQuery.data ?? new UpdateUserEmailAddress(), classvalidatorClient(UpdateUserEmailAddress)), {
        dataType: 'json',
        id: 'update-email-address',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            toast.dismiss(toastId);
            try {
                await updateEmailAddress.mutateAsync(form.data);
                toastId = toast.success('Successfully updated Account');

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }

                toastId = toast.error(form.message ?? 'Error saving email address. Please try again.');
            }
        },
        SPA: true,
        validators: classvalidatorClient(UpdateUserEmailAddress)
    });

    const updateUserForm = superForm(defaults(meQuery.data ?? new UpdateUser(), classvalidatorClient(UpdateUser)), {
        dataType: 'json',
        id: 'update-user',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            toast.dismiss(toastId);
            try {
                form.data = await updateUser.mutateAsync(form.data);
                toastId = toast.success('Successfully updated Account');

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }

                toastId = toast.error(form.message ?? 'Error saving full name. Please try again.');
            }
        },
        SPA: true,
        validators: classvalidatorClient(UpdateUser)
    });

    $effect(() => {
        if (!meQuery.isSuccess) {
            return;
        }

        if (!$updateEmailAddressFormSubmitting && !$updateEmailAddressFormTainted) {
            updateEmailAddressForm.reset({ data: meQuery.data, keepMessage: true });
        }
    });

    $effect(() => {
        if (!meQuery.isSuccess) {
            return;
        }

        if (!$updateUserFormSubmitting && !$updateUserFormTainted) {
            updateUserForm.reset({ data: meQuery.data, keepMessage: true });
        }
    });

    const {
        enhance: updateEmailAddressFormEnhance,
        form: updateEmailAddressFormData,
        message: updateEmailAddressFormMessage,
        submit: updateEmailAddressFormSubmit,
        submitting: updateEmailAddressFormSubmitting,
        tainted: updateEmailAddressFormTainted
    } = updateEmailAddressForm;
    const debouncedUpdateEmailAddressFormSubmit = debounce(1000, updateEmailAddressFormSubmit);

    const {
        enhance: updateUserFormEnhance,
        form: updateUserFormData,
        message: updateUserFormMessage,
        submit: updateUserFormSubmit,
        submitting: updateUserFormSubmitting,
        tainted: updateUserFormTainted
    } = updateUserForm;
    const debouncedUpdatedUserFormSubmit = debounce(1000, updateUserFormSubmit);

    async function handleResendVerificationEmail() {
        toast.dismiss(toastId);
        try {
            await resendVerificationEmailMutation.mutateAsync();
            toastId = toast.success('Please check your inbox for the verification email.');
        } catch {
            toastId = toast.error('Error sending verification email. Please try again.');
        }
    }
</script>

<div class="space-y-6">
    <div>
        <H3>Account</H3>
        <Muted>Manage your account settings and set e-mail preferences.</Muted>
    </div>
    <Separator />

    <Avatar.Root class="h-24 w-24" title="Profile Image">
        {#await gravatar.src}
            <Avatar.Fallback>{gravatar.initials}</Avatar.Fallback>
        {:then src}
            <Avatar.Image alt={meQuery.data ? `${meQuery.data.full_name} avatar` : 'avatar'} {src} />
        {/await}
        <Avatar.Fallback>{gravatar.initials}</Avatar.Fallback>
    </Avatar.Root>
    <Muted>Your avatar is generated by requesting a Gravatar image with the email address below.</Muted>

    <form use:updateUserFormEnhance>
        <Form.Field form={updateUserForm} name="full_name">
            <Form.Control>
                {#snippet children({ props })}
                    <Form.Label>Full Name</Form.Label>
                    <Input
                        {...props}
                        bind:value={$updateUserFormData.full_name}
                        placeholder="Full Name"
                        autocomplete="name"
                        required
                        oninput={debouncedUpdatedUserFormSubmit}
                    />
                {/snippet}
            </Form.Control>
            <Form.Description />
            <Form.FieldErrors />
            <ErrorMessage message={$updateUserFormMessage}></ErrorMessage>
        </Form.Field>
    </form>
    <form use:updateEmailAddressFormEnhance>
        <Form.Field form={updateEmailAddressForm} name="email_address">
            <Form.Control>
                {#snippet children({ props })}
                    <Form.Label>Email</Form.Label>
                    <Input
                        {...props}
                        bind:value={$updateEmailAddressFormData.email_address}
                        placeholder="Enter email address"
                        autocomplete="email"
                        required
                        oninput={debouncedUpdateEmailAddressFormSubmit}
                    />
                {/snippet}
            </Form.Control>
            <Form.Description />
            <Form.FieldErrors />
            <ErrorMessage message={$updateEmailAddressFormMessage}></ErrorMessage>
        </Form.Field>
    </form>
    {#if !isEmailAddressVerified}
        <Small>
            Email not verified. <A class="cursor-pointer" onclick={handleResendVerificationEmail}>Resend</A> verification email.
        </Small>
    {/if}
</div>
