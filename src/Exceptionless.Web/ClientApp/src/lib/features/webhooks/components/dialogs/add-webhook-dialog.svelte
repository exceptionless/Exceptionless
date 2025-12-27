<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { P } from '$comp/typography';
    import Muted from '$comp/typography/muted.svelte';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Checkbox } from '$comp/ui/checkbox';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { webhookEventTypes } from '$features/webhooks/options';
    import { type NewWebHookFormData, NewWebHookSchema } from '$features/webhooks/schemas';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';

    import type { NewWebhook, WebhookKnownEventTypes } from '../../models';

    interface Props {
        open: boolean;
        organizationId: string;
        projectId: string;
        save: (setting: NewWebhook) => Promise<void>;
    }
    let { open = $bindable(), organizationId, projectId, save }: Props = $props();

    const form = createForm(() => ({
        defaultValues: {
            event_types: [],
            organization_id: organizationId,
            project_id: projectId,
            url: ''
        } as NewWebHookFormData,
        validators: {
            onSubmit: NewWebHookSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await save(value as NewWebhook);
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
            form.setFieldValue('organization_id', organizationId);
            form.setFieldValue('project_id', projectId);
        }
    });

    function isEventTypeChecked(eventTypes: WebhookKnownEventTypes[], type: WebhookKnownEventTypes): boolean {
        return eventTypes.includes(type);
    }
</script>

{#if open}
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
                    <AlertDialog.Title>Add New Webhook</AlertDialog.Title>
                    <AlertDialog.Description>
                        Webhooks allow external services to be notified when specific events occur. Enter a URL that will be called when your selected event
                        types happen.
                    </AlertDialog.Description>
                </AlertDialog.Header>

                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                    {/snippet}
                </form.Subscribe>

                <P class="pb-4">
                    <form.Field name="url">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>URL</Field.Label>
                                <Input
                                    id={field.name}
                                    name={field.name}
                                    type="url"
                                    placeholder="Please enter a valid URL to call"
                                    autocomplete="url"
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

                    <form.Field name="event_types">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)} class="mt-4">
                                <Field.Label class="text-base">Event Types</Field.Label>
                                <Field.Description>Control when the web hook is called by choosing the event types below.</Field.Description>
                                <div class="mt-2 space-y-2">
                                    {#each webhookEventTypes as type (type.value)}
                                        {@const checked = isEventTypeChecked(field.state.value as WebhookKnownEventTypes[], type.value)}
                                        <div class="flex flex-row items-start space-x-3">
                                            <Checkbox
                                                {checked}
                                                value={type.value}
                                                onCheckedChange={(v) => {
                                                    const currentTypes = field.state.value as WebhookKnownEventTypes[];
                                                    if (v) {
                                                        field.handleChange([...currentTypes, type.value]);
                                                    } else {
                                                        field.handleChange(currentTypes.filter((i) => i !== type.value));
                                                    }
                                                }}
                                            />
                                            <div class="grid gap-1.5 leading-none">
                                                <span class="text-sm font-normal">
                                                    {type.label}
                                                </span>
                                                <Muted>{type.description}</Muted>
                                            </div>
                                        </div>
                                    {/each}
                                </div>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                </P>

                <AlertDialog.Footer>
                    <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                    <AlertDialog.Action type="submit">Add Webhook</AlertDialog.Action>
                </AlertDialog.Footer>
            </form>
        </AlertDialog.Content>
    </AlertDialog.Root>
{/if}
