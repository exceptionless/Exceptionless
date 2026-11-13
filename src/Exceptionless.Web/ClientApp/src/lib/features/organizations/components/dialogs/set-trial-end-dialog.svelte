<script lang="ts">
    import { createForm } from '@tanstack/svelte-form';
    import { z } from 'zod';

    import type { ViewOrganization } from '#features/organizations/models.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import { Muted } from '#comp/typography/index.ts';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { setTrialEndMutation } from '#features/admin/api.svelte.ts';
    import { getFormErrorMessages, getProblemMessage } from '#features/shared/validation.ts';

    let { open = $bindable(), organization }: { open: boolean; organization: ViewOrganization } = $props();
    const mutation = setTrialEndMutation();
    const form = createForm(() => ({
        defaultValues: {
            trialEndsUtc: organization.trial_ends_utc ? new Date(organization.trial_ends_utc).toISOString().slice(0, 16) : ''
        },
        validators: {
            onSubmit: z.object({
                trialEndsUtc: z.string().min(1, 'Choose a trial end date.')
            }),
            onSubmitAsync: async ({ value }) => {
                try {
                    await mutation.mutateAsync({
                        organizationId: organization.id,
                        trialEndsUtc: new Date(`${value.trialEndsUtc}Z`).toISOString()
                    });
                    open = false;
                    return null;
                } catch (error: unknown) {
                    return {
                        form: getProblemMessage(error, 'Failed to update the trial end date.')
                    };
                }
            }
        }
    }));
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <form
            onsubmit={(event) => {
                event.preventDefault();
                event.stopPropagation();
                form.handleSubmit();
            }}
        >
            <AlertDialog.Header>
                <AlertDialog.Title>Set trial end date</AlertDialog.Title>
                <AlertDialog.Description>Start or extend a trial for {organization.name}.</AlertDialog.Description>
            </AlertDialog.Header>
            <form.Field name="trialEndsUtc">
                {#snippet children(field)}
                    <Field.Field class="my-4">
                        <Field.Label for="trial-end-utc">Trial ends (UTC)</Field.Label>
                        <Input
                            id="trial-end-utc"
                            type="datetime-local"
                            required
                            value={field.state.value}
                            onblur={field.handleBlur}
                            oninput={(event) => field.handleChange(event.currentTarget.value)}
                        />
                        <Muted>A past date lets the next trial cleanup run process expiration.</Muted>
                    </Field.Field>
                {/snippet}
            </form.Field>
            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}<ErrorMessage message={getFormErrorMessages(errors)} />{/snippet}
            </form.Subscribe>
            <AlertDialog.Footer>
                <Button type="button" variant="outline" onclick={() => (open = false)} disabled={mutation.isPending}>Cancel</Button>
                <Button type="submit" disabled={mutation.isPending}>Save trial end</Button>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
