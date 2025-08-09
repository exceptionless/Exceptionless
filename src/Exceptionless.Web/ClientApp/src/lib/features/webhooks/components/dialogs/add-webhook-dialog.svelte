<script lang="ts">
    import { P } from '$comp/typography';
    import Muted from '$comp/typography/muted.svelte';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Checkbox } from '$comp/ui/checkbox';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { webhookEventTypes } from '$features/webhooks/options';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    import { NewWebhook, type WebhookKnownEventTypes } from '../../models';

    interface Props {
        open: boolean;
        organizationId: string;
        projectId: string;
        save: (setting: NewWebhook) => Promise<void>;
    }
    let { open = $bindable(), organizationId, projectId, save }: Props = $props();

    const defaultValue = new NewWebhook();
    defaultValue.organization_id = organizationId;
    defaultValue.project_id = projectId;

    const form = superForm(defaults(defaultValue, classvalidatorClient(NewWebhook)), {
        dataType: 'json',
        id: 'add-webhook',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            try {
                await save(form.data);
                open = false;

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }
            }
        },
        SPA: true,
        validators: classvalidatorClient(NewWebhook)
    });

    const { enhance, form: formData } = form;

    function addItem(eventType: WebhookKnownEventTypes) {
        $formData.event_types = [...$formData.event_types, eventType];
    }

    function removeItem(eventType: WebhookKnownEventTypes) {
        $formData.event_types = $formData.event_types.filter((i) => i !== eventType);
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form method="POST" use:enhance>
            <AlertDialog.Header>
                <AlertDialog.Title>Add New Webhook</AlertDialog.Title>
                <AlertDialog.Description>
                    Webhooks allow external services to be notified when specific events occur. Enter a URL that will be called when your selected event types
                    happen.
                </AlertDialog.Description>
            </AlertDialog.Header>

            <P class="pb-4">
                <Form.Field {form} name="url">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>URL</Form.Label>
                            <Input
                                {...props}
                                bind:value={$formData.url}
                                type="url"
                                placeholder="Please enter a valid URL to call"
                                autocomplete="url"
                                required
                            />
                        {/snippet}
                    </Form.Control>
                    <Form.Description />
                    <Form.FieldErrors />
                </Form.Field>

                <Form.Fieldset {form} name="event_types" class="space-y-0">
                    <div class="mb-4">
                        <Form.Legend class="text-base">Event Types</Form.Legend>
                        <Form.Description>Control when the web hook is called by choosing the event types below.</Form.Description>
                    </div>
                    <div class="space-y-2">
                        {#each webhookEventTypes as type (type.value)}
                            {@const checked = $formData.event_types.includes(type.value)}
                            <div class="flex flex-row items-start space-x-3">
                                <Form.Control>
                                    {#snippet children({ props })}
                                        <Checkbox
                                            {...props}
                                            {checked}
                                            value={type.value}
                                            onCheckedChange={(v) => {
                                                if (v) {
                                                    addItem(type.value);
                                                } else {
                                                    removeItem(type.value);
                                                }
                                            }}
                                        />
                                        <div class="grid gap-1.5 leading-none">
                                            <Form.Label class="font-normal">
                                                {type.label}
                                            </Form.Label>
                                            <Muted>{type.description}</Muted>
                                        </div>
                                    {/snippet}
                                </Form.Control>
                            </div>
                        {/each}
                        <Form.FieldErrors />
                    </div>
                </Form.Fieldset>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action>Add Webhook</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
