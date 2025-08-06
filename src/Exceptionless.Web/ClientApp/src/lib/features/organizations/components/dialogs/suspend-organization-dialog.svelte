<script lang="ts">
    import type { PostSuspendOrganizationParams } from '$features/organizations/api.svelte';
    import type { ViewOrganization } from '$features/organizations/models';

    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';
    import * as Form from '$comp/ui/form';
    import * as Select from '$comp/ui/select';
    import { Textarea } from '$comp/ui/textarea';
    import { SuspendOrganizationForm, SuspensionCode } from '$features/organizations/models';
    import { suspensionCodeOptions } from '$features/organizations/options';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    interface Props {
        open: boolean;
        organization: ViewOrganization;
        suspend: (params: PostSuspendOrganizationParams) => Promise<void>;
    }

    let { open = $bindable(), organization, suspend }: Props = $props();

    const form = superForm(defaults(new SuspendOrganizationForm(), classvalidatorClient(SuspendOrganizationForm)), {
        dataType: 'json',
        id: 'suspend-organization-form',
        async onUpdate({ form }) {
            if (!form.valid) {
                return;
            }

            await suspend({
                code: Number(form.data.code) as SuspensionCode,
                notes: form.data.notes
            });
            open = false;
        },
        SPA: true,
        validators: classvalidatorClient(SuspendOrganizationForm)
    });

    const { enhance, form: formData } = form;

    // Set default suspension code to Abuse
    let selectedCodeValue = $state(String(SuspensionCode.Abuse));

    $effect(() => {
        if (open && !$formData.code) {
            $formData.code = SuspensionCode.Abuse;
            selectedCodeValue = String(SuspensionCode.Abuse);
        }
    });

    function handleCodeChange(value: string | undefined) {
        if (value) {
            selectedCodeValue = value;
            $formData.code = Number(value) as SuspensionCode;
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <form method="POST" use:enhance>
            <AlertDialog.Header>
                <AlertDialog.Title>Suspend Organization</AlertDialog.Title>
                <AlertDialog.Description>
                    Are you sure you want to suspend the organization "{organization.name}"?
                </AlertDialog.Description>
            </AlertDialog.Header>

            <P class="space-y-4 pb-4">
                <Form.Field {form} name="code">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Reason for Suspension</Form.Label>
                            <Select.Root bind:value={selectedCodeValue} onValueChange={handleCodeChange} type="single" {...props}>
                                <Select.Trigger class="w-full">
                                    {suspensionCodeOptions.find((option) => String(option.value) === selectedCodeValue)?.label || 'Select a reason...'}
                                </Select.Trigger>
                                <Select.Content>
                                    {#each suspensionCodeOptions as option (option.value)}
                                        <Select.Item value={String(option.value)}>{option.label}</Select.Item>
                                    {/each}
                                </Select.Content>
                            </Select.Root>
                        {/snippet}
                    </Form.Control>
                    <Form.Description>Select a reason for suspending this organization.</Form.Description>
                    <Form.FieldErrors />
                </Form.Field>

                <Form.Field {form} name="notes">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Additional Details (Optional)</Form.Label>
                            <Textarea {...props} bind:value={$formData.notes} placeholder="Add any relevant context or details (optional)..." rows={3} />
                        {/snippet}
                    </Form.Control>
                    <Form.Description>Provide any relevant context or notes about this suspension.</Form.Description>
                    <Form.FieldErrors />
                </Form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action type="submit" class={buttonVariants({ variant: 'destructive' })}>Suspend Organization</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
