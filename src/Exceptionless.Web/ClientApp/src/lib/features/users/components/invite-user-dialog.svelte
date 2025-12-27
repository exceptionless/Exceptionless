<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';

    import { type InviteUserFormData, InviteUserSchema } from '../schemas';

    interface Props {
        inviteUser: (email: string) => Promise<void>;
        open: boolean;
    }

    let { inviteUser, open = $bindable() }: Props = $props();

    const form = createForm(() => ({
        defaultValues: {
            email: ''
        } as InviteUserFormData,
        validators: {
            onSubmit: InviteUserSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await inviteUser(value.email);
                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }
                    return { form: 'An unexpected error occurred, please try again.' };
                }
            }
        }
    }));

    $effect(() => {
        if (open) {
            form.reset();
        }
    });
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                form.handleSubmit();
            }}
        >
            <AlertDialog.Header>
                <AlertDialog.Title>Invite User</AlertDialog.Title>
                <AlertDialog.Description>Enter the email address of the user you want to invite to this organization.</AlertDialog.Description>
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>

            <P class="pb-4">
                <form.Field name="email">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Email Address</Field.Label>
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
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action type="submit">Invite User</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
