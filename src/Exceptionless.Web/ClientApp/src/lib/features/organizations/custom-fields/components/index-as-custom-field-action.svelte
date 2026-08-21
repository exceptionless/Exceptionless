<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import * as Field from '$comp/ui/field';
    import * as Select from '$comp/ui/select';
    import { Spinner } from '$comp/ui/spinner';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import {
        createCustomFieldMutation,
        type CustomFieldDefinition,
        CustomFieldNameSchema,
        INDEX_TYPE_DESCRIPTIONS,
        INDEX_TYPE_LABELS,
        INDEX_TYPES,
        parseIndexType,
        type QuickCreateCustomFieldFormData,
        QuickCreateCustomFieldSchema
    } from '$features/organizations/custom-fields';
    import { getFormErrorMessages, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    interface Props {
        customFields: CustomFieldDefinition[];
        fieldName: string;
        open?: boolean;
        organizationId: string;
    }

    let { customFields, fieldName, open = $bindable(false), organizationId }: Props = $props();

    const isReservedSystemField = $derived(['haserror', 'sessionend'].includes(fieldName.toLowerCase()));
    const isAlreadyIndexed = $derived(customFields.some((field) => field.name.toLowerCase() === fieldName.toLowerCase()));
    const canCreate = $derived(!isReservedSystemField && CustomFieldNameSchema.safeParse(fieldName).success && !isAlreadyIndexed);

    const createField = createCustomFieldMutation({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const form = createForm(() => ({
        defaultValues: {
            indexType: 'keyword'
        } as QuickCreateCustomFieldFormData,
        validators: {
            onSubmit: QuickCreateCustomFieldSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await createField.mutateAsync({
                        indexType: value.indexType,
                        name: fieldName
                    });
                    toast.success(`"${fieldName}" is now indexed as a custom field. Future events will include it in search.`);
                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (showBillingDialogOnUpgradeProblem(error, organizationId, () => form.handleSubmit())) {
                        return null;
                    }

                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'An unexpected error occurred.'
                    };
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

{#if organizationId && canCreate}
    <Dialog.Root bind:open>
        <Dialog.Content class="sm:max-w-[400px]">
            <Dialog.Header>
                <Dialog.Title>Index "{fieldName}" as Custom Field</Dialog.Title>
                <Dialog.Description>
                    This will start indexing "{fieldName}" from future events, making it available for filtering and search. Existing events will not be
                    retroactively indexed.
                </Dialog.Description>
            </Dialog.Header>

            <form
                class="flex flex-col gap-4"
                onsubmit={(e) => {
                    e.preventDefault();
                    form.handleSubmit();
                }}
            >
                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)} />
                    {/snippet}
                </form.Subscribe>

                <form.Field name="indexType">
                    {#snippet children(field)}
                        <Field.Field>
                            <Field.Label for={field.name}>Index Type</Field.Label>
                            <Select.Root type="single" value={field.state.value} onValueChange={(value) => field.handleChange(parseIndexType(value))}>
                                <Select.Trigger id={field.name} class="w-full">
                                    <span class="font-medium">{INDEX_TYPE_LABELS[field.state.value]}</span>
                                    <span class="text-muted-foreground ml-2 text-xs">{INDEX_TYPE_DESCRIPTIONS[field.state.value]}</span>
                                </Select.Trigger>
                                <Select.Content class="max-h-72">
                                    <Select.Group>
                                        {#each INDEX_TYPES as type (type)}
                                            <Select.Item value={type} class="flex flex-col items-start gap-0.5 py-2">
                                                <span class="font-medium">{INDEX_TYPE_LABELS[type]}</span>
                                                <span class="text-muted-foreground text-xs">{INDEX_TYPE_DESCRIPTIONS[type]}</span>
                                            </Select.Item>
                                        {/each}
                                    </Select.Group>
                                </Select.Content>
                            </Select.Root>
                            <Muted>Choose the type that best matches this field's data.</Muted>
                        </Field.Field>
                    {/snippet}
                </form.Field>

                <Dialog.Footer>
                    <Button variant="outline" type="button" onclick={() => (open = false)}>Cancel</Button>
                    <form.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <Button type="submit" disabled={isSubmitting}>
                                {#if isSubmitting}
                                    <Spinner data-icon="inline-start" />
                                {/if}
                                Index Field
                            </Button>
                        {/snippet}
                    </form.Subscribe>
                </Dialog.Footer>
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}
