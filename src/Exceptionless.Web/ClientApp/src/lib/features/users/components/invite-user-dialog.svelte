<script lang="ts">
    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    import { InviteUserForm } from '../models';

    interface Props {
        inviteUser: (email: string) => Promise<void>;
        open: boolean;
    }

    let { inviteUser, open = $bindable() }: Props = $props();

    const form = superForm(defaults(new InviteUserForm(), classvalidatorClient(InviteUserForm)), {
        dataType: 'json',
        async onUpdate({ form }) {
            if (!form.valid) {
                return;
            }

            await inviteUser(form.data.email);
            open = false;
        },
        SPA: true,
        validators: classvalidatorClient(InviteUserForm)
    });

    const { enhance, form: formData } = form;
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form method="POST" use:enhance>
            <AlertDialog.Header>
                <AlertDialog.Title>Invite User</AlertDialog.Title>
                <AlertDialog.Description>Enter the email address of the user you want to invite to this organization.</AlertDialog.Description>
            </AlertDialog.Header>

            <P class="pb-4">
                <Form.Field {form} name="email">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Email Address</Form.Label>
                            <Input {...props} bind:value={$formData.email} type="email" placeholder="user@example.com" autocomplete="email" required />
                        {/snippet}
                    </Form.Control>
                    <Form.Description />
                    <Form.FieldErrors />
                </Form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action>Invite User</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
