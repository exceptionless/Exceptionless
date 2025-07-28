<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { A, H4, Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { NotificationSettings } from '$features/projects/models';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import InfoIcon from '@lucide/svelte/icons/info';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';
    import { debounce } from 'throttle-debounce';

    interface Props {
        emailNotificationsEnabled?: boolean;
        hasPremiumFeatures?: boolean;
        save: (settings: NotificationSettings) => Promise<void>;
        settings?: NotificationSettings;
        upgrade?: () => Promise<void>;
    }

    let { emailNotificationsEnabled = true, hasPremiumFeatures = false, save, settings, upgrade }: Props = $props();
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

    const { enhance, form: formData, message, submit, submitting, tainted } = form;
    const debouncedFormSubmit = debounce(500, () => submit());

    $effect(() => {
        if (settings && !$submitting && !$tainted) {
            form.reset({ data: settings, keepMessage: true });
        }
    });
</script>

{#if $formData.send_daily_summary !== undefined}
    <form method="POST" use:enhance class="space-y-6">
        <ErrorMessage message={$message} />

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Daily Project Summary</div>
                    <Muted class="text-xs">Receive a daily summary of your project activity.</Muted>
                </div>
                <Switch
                    id="send_daily_summary"
                    bind:checked={$formData.send_daily_summary}
                    onclick={debouncedFormSubmit}
                    disabled={!emailNotificationsEnabled}
                />
            </div>
        </div>

        <div class="space-y-4">
            <H4>Event Notifications</H4>
            <div class="space-y-3">
                {#if !hasPremiumFeatures}
                    <Alert.Root class="border-blue-200 bg-blue-50 dark:border-blue-900/30 dark:bg-blue-900/10">
                        <InfoIcon class="h-4 w-4 text-blue-600 dark:text-blue-400" />
                        <Alert.Title class="text-blue-900 dark:text-blue-100">
                            <A onclick={upgrade}>Upgrade now</A> to enable occurrence level notifications!
                        </Alert.Title>
                    </Alert.Root>
                {/if}
                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">New Errors</div>
                            <Muted class="text-xs">Notify me when new errors occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_new_errors"
                            bind:checked={$formData.report_new_errors}
                            onclick={debouncedFormSubmit}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">Critical Errors</div>
                            <Muted class="text-xs">Notify me when critical errors occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_critical_errors"
                            bind:checked={$formData.report_critical_errors}
                            onclick={debouncedFormSubmit}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">Error Regressions</div>
                            <Muted class="text-xs">Notify me when errors regress in this project.</Muted>
                        </div>
                        <Switch
                            id="report_event_regressions"
                            bind:checked={$formData.report_event_regressions}
                            onclick={debouncedFormSubmit}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">New Events</div>
                            <Muted class="text-xs">Notify me when new events occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_new_events"
                            bind:checked={$formData.report_new_events}
                            onclick={debouncedFormSubmit}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">Critical Events</div>
                            <Muted class="text-xs">Notify me when critical events occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_critical_events"
                            bind:checked={$formData.report_critical_events}
                            onclick={debouncedFormSubmit}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>
            </div>
        </div>
    </form>
{:else}
    <div class="space-y-6">
        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div class="space-y-1">
                    <Skeleton class="h-5 w-32 rounded" />
                    <Skeleton class="h-4 w-48 rounded" />
                </div>
                <Skeleton class="h-[1.15rem] w-8 rounded-full" />
            </div>
        </div>

        <div class="space-y-4">
            <Skeleton class="h-6 w-40 rounded" />
            <div class="space-y-3">
                {#each { length: 5 } as name, index (`${name}-${index}`)}
                    <div class="rounded-lg border p-4 opacity-60">
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
        </div>
    </div>
{/if}
