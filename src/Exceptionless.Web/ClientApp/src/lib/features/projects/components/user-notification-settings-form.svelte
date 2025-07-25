<script lang="ts">
    import type { Snippet } from "svelte";

    import ErrorMessage from '$comp/error-message.svelte';
    import { H4, Muted } from '$comp/typography';
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
        children?: Snippet;
        save: (settings: NotificationSettings) => Promise<void>;
        settings?: NotificationSettings;
    }

    let { children, save, settings }: Props = $props();
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

{#if $formData.send_daily_summary !== undefined}
        <form method="POST" use:enhance class="space-y-2">
            <ErrorMessage message={$message} />
            <div class="rounded-lg border p-4 flex flex-col divide-y divide-border bg-card">
                {#if children}
                    {@render children()}
                {/if}
                <div class="flex flex-row items-center justify-between py-2">
                    <div class="space-y-0.5">
                        <H4>Daily Project Summary</H4>
                        <Muted>Receive a daily summary of your project activity.</Muted>
                    </div>
                    <Switch id="send_daily_summary" bind:checked={$formData.send_daily_summary} onclick={debouncedFormSubmit} />
                </div>
                <div class="flex flex-row items-center justify-between py-2">
                    <div class="space-y-0.5">
                        <H4>New Errors</H4>
                        <Muted>Notify me on new errors.</Muted>
                    </div>
                    <Switch id="report_new_errors" bind:checked={$formData.report_new_errors} onclick={debouncedFormSubmit} />
                </div>
                <div class="flex flex-row items-center justify-between py-2">
                    <div class="space-y-0.5">
                        <H4>Critical Errors</H4>
                        <Muted>Notify me on critical errors.</Muted>
                    </div>
                    <Switch id="report_critical_errors" bind:checked={$formData.report_critical_errors} onclick={debouncedFormSubmit} />
                </div>
                <div class="flex flex-row items-center justify-between py-2">
                    <div class="space-y-0.5">
                        <H4>Error Regressions</H4>
                        <Muted>Notify me on error regressions.</Muted>
                    </div>
                    <Switch id="report_event_regressions" bind:checked={$formData.report_event_regressions} onclick={debouncedFormSubmit} />
                </div>
                <div class="flex flex-row items-center justify-between py-2">
                    <div class="space-y-0.5">
                        <H4>New Events</H4>
                        <Muted>Notify me on new events.</Muted>
                    </div>
                    <Switch id="report_new_events" bind:checked={$formData.report_new_events} onclick={debouncedFormSubmit} />
                </div>
                <div class="flex flex-row items-center justify-between py-2">
                    <div class="space-y-0.5">
                        <H4>Critical Events</H4>
                        <Muted>Notify me on critical events.</Muted>
                    </div>
                    <Switch id="report_critical_events" bind:checked={$formData.report_critical_events} onclick={debouncedFormSubmit} />
                </div>
            </div>
        </form>
{:else}
    <div class="rounded-lg border p-4 flex flex-col divide-y divide-border bg-card">
        {#each { length: 6 } as name, index (`${name}-${index}`)}
            <div class="flex flex-row items-center justify-between py-2">
                <div class="space-y-1">
                    <Skeleton class="h-6 w-40 rounded-md" />
                    <Skeleton class="h-5 w-64 rounded" />
                </div>
                <Skeleton class="h-[1.15rem] w-8 rounded-full" />
            </div>
        {/each}
    </div>
{/if}
