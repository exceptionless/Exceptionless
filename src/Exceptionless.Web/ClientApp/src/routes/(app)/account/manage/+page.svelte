<script lang="ts">
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import Loading from '$comp/Loading.svelte';
    import { A, H3, Muted, Small } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { getMeQuery, patchUser, postEmailAddress } from '$features/users/api.svelte';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import { UpdateUser, User } from '$features/users/models';
    import { applyServerSideErrors } from '$shared/validation';
    import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();
    const userResponse = getMeQuery();
    const isEmailAddressVerified = $derived(userResponse.data?.is_email_address_verified ?? false);
    const gravatar = getGravatarFromCurrentUser(userResponse);
    const updateUser = patchUser({
        route: {
            get id() {
                return userResponse.data?.id;
            }
        }
    });

    const updateEmailAddress = postEmailAddress({
        route: {
            get id() {
                return userResponse.data?.id;
            }
        }
    });

    const updateEmailAddressForm = superForm(defaults(userResponse.data ?? new User(), classvalidatorClient(User)), {
        dataType: 'json',
        id: 'update-email-address',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            toast.dismiss(toastId);
            try {
                await updateEmailAddress.mutateAsync(form.data);
                toastId = toast.success('Account updated successfully.');

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    // https://github.com/ciscoheat/sveltekit-superforms/issues/536
                    updateEmailAddressFormMessage.set(form.message);
                    result.status = error.status ?? 500;
                    toastId = toast.error(form.message ?? 'Error saving email address. Please try again.');
                }
            }
        },
        SPA: true,
        validators: classvalidatorClient(User)
    });

    const updateUserForm = superForm(defaults(userResponse.data ?? new UpdateUser(), classvalidatorClient(UpdateUser)), {
        dataType: 'json',
        id: 'update-user',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            toast.dismiss(toastId);
            try {
                form.data = await updateUser.mutateAsync(form.data);
                toastId = toast.success('Account updated successfully.');

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    // https://github.com/ciscoheat/sveltekit-superforms/issues/536
                    updateUserFormMessage.set(form.message);
                    result.status = error.status ?? 500;
                    toastId = toast.error(form.message ?? 'Error saving full name. Please try again.');
                }
            }
        },
        SPA: true,
        validators: classvalidatorClient(UpdateUser)
    });

    $effect(() => {
        if (!userResponse.isSuccess) {
            return;
        }

        if (!$updateEmailAddressFormSubmitting && !$updateEmailAddressFormTainted) {
            updateEmailAddressForm.reset({ data: userResponse.data, keepMessage: true });
        }

        if (!$updateUserFormSubmitting && !$updateUserFormTainted) {
            updateUserForm.reset({ data: userResponse.data, keepMessage: true });
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

    async function resendVerificationEmail() {
        toast.dismiss(toastId);
        const client = useFetchClient();
        try {
            await client.get(`users/${userResponse.data?.id}/resend-verification-email`);
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
            <Avatar.Fallback><Loading /></Avatar.Fallback>
        {:then src}
            <Avatar.Image alt="gravatar" {src} />
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
            Email not verified. <A class="cursor-pointer" onclick={resendVerificationEmail}>Resend</A> verification email.
        </Small>
    {/if}
</div>
