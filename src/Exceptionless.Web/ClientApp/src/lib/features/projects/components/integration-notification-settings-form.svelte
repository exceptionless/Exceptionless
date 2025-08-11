<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { NotificationSettings } from '$features/projects/models';
    import { structuredCloneState } from '$features/shared/utils/state.svelte';
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
    let previousSettingsRef = $state<NotificationSettings>();

    const form = superForm(defaults(structuredCloneState(settings) || new NotificationSettings(), classvalidatorClient(NotificationSettings)), {
        dataType: 'json',
        id: 'integration-notification-settings',
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

    const { enhance, form: formData, message, submit, submitting, tainted } = form;
    const debouncedFormSubmit = debounce(500, () => submit());

    $effect(() => {
        if (!$submitting && !$tainted && settings !== previousSettingsRef) {
            const clonedSettings = structuredCloneState(settings);
            form.reset({ data: clonedSettings, keepMessage: true });
            previousSettingsRef = settings;
        }
    });
</script>

{#if $formData.send_daily_summary !== undefined}
    <form method="POST" use:enhance class="space-y-3">
        <ErrorMessage message={$message} />

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">New Errors</div>
                    <Muted class="text-xs">Notify me when new errors occur in this project.</Muted>
                </div>
                <Switch id="report_new_errors" bind:checked={$formData.report_new_errors} onclick={debouncedFormSubmit} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Critical Errors</div>
                    <Muted class="text-xs">Notify me when critical errors occur in this project.</Muted>
                </div>
                <Switch id="report_critical_errors" bind:checked={$formData.report_critical_errors} onclick={debouncedFormSubmit} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Error Regressions</div>
                    <Muted class="text-xs">Notify me when errors regress in this project.</Muted>
                </div>
                <Switch id="report_event_regressions" bind:checked={$formData.report_event_regressions} onclick={debouncedFormSubmit} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">New Events</div>
                    <Muted class="text-xs">Notify me when new events occur in this project.</Muted>
                </div>
                <Switch id="report_new_events" bind:checked={$formData.report_new_events} onclick={debouncedFormSubmit} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Critical Events</div>
                    <Muted class="text-xs">Notify me when critical events occur in this project.</Muted>
                </div>
                <Switch id="report_critical_events" bind:checked={$formData.report_critical_events} onclick={debouncedFormSubmit} />
            </div>
        </div>
    </form>
{:else}
    <div class="space-y-3">
        {#each { length: 5 } as name, index (`${name}-${index}`)}
            <div class="rounded-lg border p-4">
                <div class="flex items-center justify-between">
                    <div class="space-y-1">
                        <Skeleton class="h-5 w-32 rounded" />
                        <Skeleton class="h-4 w-48 rounded" />
                    </div>
                    <Skeleton class="h-[1.15rem] w-8 rounded-full" />
                </div>
            </div>
        {/each}
    </div>
{/if}
