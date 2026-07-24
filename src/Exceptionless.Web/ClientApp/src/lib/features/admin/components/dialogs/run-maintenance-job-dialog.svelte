<script lang="ts">
    import type { RunMaintenanceJobParams } from '$features/admin/api.svelte';
    import type { MaintenanceAction } from '$features/admin/models';

    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as Calendar from '$comp/ui/calendar';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import { type RunMaintenanceJobFormData, RunMaintenanceJobSchema } from '$features/admin/schemas';
    import { formatDateLabel } from '$features/shared/dates';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { CalendarDate, type DateValue } from '@internationalized/date';
    import CalendarIcon from '@lucide/svelte/icons/calendar';
    import TriangleAlert from '@lucide/svelte/icons/triangle-alert';
    import { createForm } from '@tanstack/svelte-form';

    interface Props {
        action: MaintenanceAction;
        onConfirm: (params: RunMaintenanceJobParams) => Promise<void>;
        open: boolean;
        organizationId?: string;
    }

    let { action, onConfirm, open = $bindable(), organizationId }: Props = $props();

    let utcStartCalendar = $state<CalendarDate | undefined>();
    let utcEndCalendar = $state<CalendarDate | undefined>();
    let startCalendarOpen = $state(false);
    let endCalendarOpen = $state(false);

    const form = createForm(() => ({
        defaultValues: {
            confirmText: '',
            organizationId: '',
            utcEnd: undefined,
            utcStart: undefined
        } as RunMaintenanceJobFormData,
        validators: {
            onSubmit: RunMaintenanceJobSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await onConfirm({
                        name: action.name,
                        organizationId: organizationId || value.organizationId || undefined,
                        utcEnd: value.utcEnd,
                        utcStart: value.utcStart
                    });

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
            utcStartCalendar = undefined;
            utcEndCalendar = undefined;
        }
    });

    function handleStartCalendarChange(value: DateValue | undefined) {
        if (value) {
            utcStartCalendar = new CalendarDate(value.year, value.month, value.day);
            form.setFieldValue('utcStart', new Date(Date.UTC(value.year, value.month - 1, value.day, 0, 0, 0, 0)));
        } else {
            utcStartCalendar = undefined;
            form.setFieldValue('utcStart', undefined);
        }
    }

    function handleEndCalendarChange(value: DateValue | undefined) {
        if (value) {
            utcEndCalendar = new CalendarDate(value.year, value.month, value.day);
            form.setFieldValue('utcEnd', new Date(Date.UTC(value.year, value.month - 1, value.day, 23, 59, 59, 999)));
        } else {
            utcEndCalendar = undefined;
            form.setFieldValue('utcEnd', undefined);
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
                <AlertDialog.Title>{action.label}</AlertDialog.Title>
                <AlertDialog.Description>{action.description}</AlertDialog.Description>
            </AlertDialog.Header>

            <div class="space-y-4 py-4">
                {#if action.dangerous}
                    <div class="border-destructive/50 bg-destructive/10 text-destructive flex items-start gap-2 rounded-md border p-3 text-sm">
                        <TriangleAlert class="mt-0.5 size-4 shrink-0" />
                        <span>This is a destructive operation. Please ensure you understand the impact before proceeding.</span>
                    </div>
                {/if}

                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)} />
                    {/snippet}
                </form.Subscribe>

                {#if action.hasDateRange}
                    <div class="grid grid-cols-2 gap-3">
                        <Field.Field>
                            <Field.Label>Start Date (UTC)</Field.Label>
                            <Popover.Root bind:open={startCalendarOpen}>
                                <Popover.Trigger>
                                    {#snippet child({ props: triggerProps })}
                                        <Button {...triggerProps} variant="outline" class="w-full justify-start text-left font-normal" type="button">
                                            <CalendarIcon class="mr-2 size-4" />
                                            {utcStartCalendar
                                                ? formatDateLabel(new Date(Date.UTC(utcStartCalendar.year, utcStartCalendar.month - 1, utcStartCalendar.day)))
                                                : 'Select start date...'}
                                        </Button>
                                    {/snippet}
                                </Popover.Trigger>
                                <Popover.Content class="w-auto p-0">
                                    <Calendar.Calendar
                                        value={utcStartCalendar}
                                        onValueChange={handleStartCalendarChange}
                                        type="single"
                                        calendarLabel="Select a start date"
                                        locale="en-US"
                                    />
                                </Popover.Content>
                            </Popover.Root>
                            <Muted>Leave blank to use the default start date.</Muted>
                        </Field.Field>

                        <Field.Field>
                            <Field.Label>End Date (UTC)</Field.Label>
                            <Popover.Root bind:open={endCalendarOpen}>
                                <Popover.Trigger>
                                    {#snippet child({ props: triggerProps })}
                                        <Button {...triggerProps} variant="outline" class="w-full justify-start text-left font-normal" type="button">
                                            <CalendarIcon class="mr-2 size-4" />
                                            {utcEndCalendar
                                                ? formatDateLabel(new Date(Date.UTC(utcEndCalendar.year, utcEndCalendar.month - 1, utcEndCalendar.day)))
                                                : 'Select end date...'}
                                        </Button>
                                    {/snippet}
                                </Popover.Trigger>
                                <Popover.Content class="w-auto p-0">
                                    <Calendar.Calendar
                                        value={utcEndCalendar}
                                        onValueChange={handleEndCalendarChange}
                                        type="single"
                                        calendarLabel="Select an end date"
                                        locale="en-US"
                                    />
                                </Popover.Content>
                            </Popover.Root>
                            <Muted>Leave blank to process through the current date.</Muted>
                        </Field.Field>
                    </div>
                {/if}

                {#if action.hasOrganizationId && !organizationId}
                    <form.Field name="organizationId">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Organization ID</Field.Label>
                                <Input
                                    id={field.name}
                                    placeholder="Leave blank to process all organizations..."
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                <Muted>Restrict this job to a specific organization.</Muted>
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                {/if}

                <form.Field
                    name="confirmText"
                    validators={{ onChange: ({ value }) => (value === action.name ? undefined : `Type "${action.name}" to confirm`) }}
                >
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>
                                Type <strong>{action.name}</strong> to confirm
                            </Field.Label>
                            <Input
                                id={field.name}
                                autocomplete="off"
                                placeholder={action.name}
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(e.currentTarget.value)}
                                aria-invalid={ariaInvalid(field)}
                            />
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </div>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <form.Subscribe selector={(state) => state.isSubmitting || state.values.confirmText !== action.name}>
                    {#snippet children(isDisabled)}
                        <AlertDialog.Action
                            type="submit"
                            class={buttonVariants({ variant: action.dangerous ? 'destructive' : 'default' })}
                            disabled={isDisabled}
                        >
                            {isDisabled && form.state.isSubmitting ? 'Running...' : 'Run Job'}
                        </AlertDialog.Action>
                    {/snippet}
                </form.Subscribe>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
