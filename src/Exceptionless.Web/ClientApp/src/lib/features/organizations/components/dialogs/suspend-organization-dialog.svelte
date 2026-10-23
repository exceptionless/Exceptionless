<script lang="ts">
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { createForm } from '@tanstack/svelte-form';

    import type { PostSuspendOrganizationParams } from '#features/organizations/api.svelte.ts';
    import type { ViewOrganization } from '#features/organizations/models.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import { P } from '#comp/typography/index.ts';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { buttonVariants } from '#comp/ui/button/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import * as Select from '#comp/ui/select/index.ts';
    import { Textarea } from '#comp/ui/textarea/index.ts';
    import { SuspensionCode } from '#features/organizations/models.ts';
    import { suspensionCodeOptions } from '#features/organizations/options.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#features/shared/validation.ts';

    import { type SuspendOrganizationFormData, SuspendOrganizationSchema } from '../../schemas';

    interface Props {
        open: boolean;
        organization: ViewOrganization;
        suspend: (params: PostSuspendOrganizationParams) => Promise<void>;
    }

    let { open = $bindable(), organization, suspend }: Props = $props();

    // Set default suspension code to Abuse
    let selectedCodeValue = $state(String(SuspensionCode.Abuse));

    const form = createForm(() => ({
        defaultValues: {
            code: SuspensionCode.Abuse,
            notes: ''
        } as SuspendOrganizationFormData,
        validators: {
            onSubmit: SuspendOrganizationSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await suspend({
                        code: value.code,
                        notes: value.notes
                    });

                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'An unexpected error occurred, please try again.'
                    };
                }
            }
        }
    }));

    $effect(() => {
        if (open) {
            form.reset();
            selectedCodeValue = String(SuspensionCode.Abuse);
        }
    });

    function handleCodeChange(value: string | undefined) {
        if (value) {
            selectedCodeValue = value;
            form.setFieldValue('code', Number(value) as SuspensionCode);
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <form
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                form.handleSubmit();
            }}
        >
            <AlertDialog.Header>
                <AlertDialog.Title>Suspend Organization</AlertDialog.Title>
                <AlertDialog.Description>
                    Are you sure you want to suspend the organization "{organization.name}"?
                </AlertDialog.Description>
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>

            <P class="space-y-4 pb-4">
                <form.Field name="code">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Reason for Suspension</Field.Label>
                            <Select.Root bind:value={selectedCodeValue} onValueChange={handleCodeChange} type="single">
                                <Select.Trigger id={field.name} class="w-full">
                                    {suspensionCodeOptions.find((option) => String(option.value) === selectedCodeValue)?.label || 'Select a reason...'}
                                </Select.Trigger>
                                <Select.Content>
                                    {#each suspensionCodeOptions as option (option.value)}
                                        <Select.Item value={String(option.value)}>{option.label}</Select.Item>
                                    {/each}
                                </Select.Content>
                            </Select.Root>
                            <Field.Description>Select a reason for suspending this organization.</Field.Description>
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>

                <form.Field name="notes">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Additional Details (Optional)</Field.Label>
                            <Textarea
                                id={field.name}
                                name={field.name}
                                placeholder="Add any relevant context or details..."
                                rows={3}
                                value={field.state.value ?? ''}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(e.currentTarget.value)}
                                aria-invalid={ariaInvalid(field)}
                            />
                            <Field.Description>Provide any relevant context or notes about this suspension.</Field.Description>
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action
                    type="submit"
                    class={buttonVariants({
                        variant: 'destructive'
                    })}>Suspend Organization</AlertDialog.Action
                >
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
