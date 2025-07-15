<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Label } from '$comp/ui/label';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { NotificationSettings } from '$features/projects/models';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';
    import { debounce } from 'throttle-debounce';

    interface Props {
        save: (settings: NotificationSettings) => Promise<void>;
        settings?: NotificationSettings;
    }

    let { save, settings }: Props = $props();
    let toastId = $state<number | string>();

    const form = superForm(defaults(settings || new NotificationSettings(), classvalidatorClient(NotificationSettings)), {
        dataType: 'json',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            toast.dismiss(toastId);
            if (save) {
                try {
                    await save(form.data);

                    // HACK: This is to prevent sveltekit from stealing focus
                    result.type = 'failure';
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        applyServerSideErrors(form, error);
                        result.status = error.status ?? 500;
                    } else {
                        result.status = 500;
                    }

                    toastId = toast.error(form.message ?? 'Error saving notification settings. Please try again.');
                }
            }
        },
        SPA: true,
        validators: classvalidatorClient(NotificationSettings)
    });

    // TODO: Use the Switch primitive component?
    const { enhance, form: formData, message, submit, submitting, tainted } = form;
    const debouncedFormSubmit = debounce(500, () => submit());

    $effect(() => {
        if (settings && !$submitting && !$tainted) {
            form.reset({ data: settings, keepMessage: true });
        }
    });
</script>

{#if $formData}
    <form method="POST" use:enhance class="space-y-2">
        <ErrorMessage message={$message} />

        <div class="flex items-center space-x-2">
            <Switch id="send_daily_summary" bind:checked={$formData.send_daily_summary} onCheckedChange={debouncedFormSubmit} />
            <Label for="send_daily_summary">
                Send daily project summary <strong>(Coming soon!)</strong>
            </Label>
        </div>

        <div class="flex items-center space-x-2">
            <Switch id="report_new_errors" bind:checked={$formData.report_new_errors} onCheckedChange={debouncedFormSubmit} />
            <Label for="report_new_errors">Notify me on new errors</Label>
        </div>

        <div class="flex items-center space-x-2">
            <Switch id="report_critical_errors" bind:checked={$formData.report_critical_errors} onCheckedChange={debouncedFormSubmit} />
            <Label for="report_critical_errors">Notify me on critical errors</Label>
        </div>

        <div class="flex items-center space-x-2">
            <Switch id="report_event_regressions" bind:checked={$formData.report_event_regressions} onCheckedChange={debouncedFormSubmit} />
            <Label for="report_event_regressions">Notify me on error regressions</Label>
        </div>

        <div class="flex items-center space-x-2">
            <Switch id="report_new_events" bind:checked={$formData.report_new_events} onCheckedChange={debouncedFormSubmit} />
            <Label for="report_new_events">Notify me on new events</Label>
        </div>

        <div class="flex items-center space-x-2">
            <Switch id="report_critical_events" bind:checked={$formData.report_critical_events} onCheckedChange={debouncedFormSubmit} />
            <Label for="report_critical_events">Notify me on critical events</Label>
        </div>
    </form>
{:else}
    <div class="space-y-4">
        {#each { length: 6 } as name, index (`${name}-${index}`)}
            <div class="flex items-center space-x-2">
                <Skeleton class="size-5 rounded-sm" />
                <Skeleton class="size-5 w-64 rounded-md" />
            </div>
        {/each}
    </div>
{/if}
