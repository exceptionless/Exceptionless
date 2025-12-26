<script lang="ts">
    import type { PostSetBonusOrganizationParams } from '$features/organizations/api.svelte';
    import type { ViewOrganization } from '$features/organizations/models';

    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted, P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as Calendar from '$comp/ui/calendar';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import { SetBonusOrganizationSchema } from '$features/organizations/schemas';
    import Number from '$features/shared/components/formatters/number.svelte';
    import { formatDateLabel } from '$features/shared/dates';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { CalendarDate, type DateValue } from '@internationalized/date';
    import CalendarIcon from '@lucide/svelte/icons/calendar';
    import { createForm } from '@tanstack/svelte-form';

    interface Props {
        open: boolean;
        organization: ViewOrganization;
        setBonus: (params: PostSetBonusOrganizationParams) => Promise<void>;
    }

    let { open = $bindable(), organization, setBonus }: Props = $props();

    let calendarValue = $state<CalendarDate | undefined>();
    let calendarOpen = $state(false);

    function getDefaultValues(): { bonusEvents: number; calendarDate: CalendarDate; expires: Date | undefined } {
        if ((organization.bonus_events_per_month ?? 0) > 0) {
            const expirationDate = organization.bonus_expiration ? new Date(organization.bonus_expiration) : undefined;
            if (expirationDate) {
                return {
                    bonusEvents: organization.bonus_events_per_month ?? 0,
                    calendarDate: new CalendarDate(expirationDate.getFullYear(), expirationDate.getMonth() + 1, expirationDate.getDate()),
                    expires: expirationDate
                };
            }
        }

        // Set default values: 20% of plan limit and first day of next month
        const defaultBonus = Math.round((organization.max_events_per_month ?? 0) * 0.2);
        const now = new Date();
        const nextMonth = new Date(now.getFullYear(), now.getMonth() + 1, 1, 0, 0, 0, 0);

        return {
            bonusEvents: defaultBonus,
            calendarDate: new CalendarDate(nextMonth.getFullYear(), nextMonth.getMonth() + 1, nextMonth.getDate()),
            expires: nextMonth
        };
    }

    // Get initial defaults for form creation
    const initialDefaults = getDefaultValues();

    const form = createForm(() => ({
        defaultValues: {
            bonusEvents: initialDefaults.bonusEvents,
            expires: initialDefaults.expires
        } as { bonusEvents: number; expires?: Date },
        validators: {
            onSubmit: SetBonusOrganizationSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await setBonus({
                        bonusEvents: value.bonusEvents,
                        expires: value.expires,
                        organizationId: organization.id!
                    });

                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }
                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    // Reset form and initialize calendar when dialog opens
    $effect(() => {
        if (open) {
            const defaults = getDefaultValues();
            form.reset();
            form.setFieldValue('bonusEvents', defaults.bonusEvents);
            form.setFieldValue('expires', defaults.expires);
            calendarValue = defaults.calendarDate;
        }
    });

    // Sync calendar selection to form (only when user picks a date, not during initialization)
    function handleCalendarChange(value: DateValue | undefined) {
        if (value) {
            calendarValue = new CalendarDate(value.year, value.month, value.day);
            const expires = new Date(Date.UTC(value.year, value.month - 1, value.day));
            form.setFieldValue('expires', expires);
        } else {
            calendarValue = undefined;
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
                <AlertDialog.Title>Set Bonus Events</AlertDialog.Title>
                <AlertDialog.Description>
                    Grant additional event capacity to the organization "{organization.name}".
                </AlertDialog.Description>
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>

            <P class="space-y-4 pb-4">
                <form.Field name="bonusEvents">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Bonus Events</Field.Label>
                            <Input
                                id={field.name}
                                name={field.name}
                                type="number"
                                min="0"
                                step="1"
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(parseInt(e.currentTarget.value) || 0)}
                                placeholder="Enter number of bonus events to add"
                                aria-invalid={ariaInvalid(field)}
                            />
                            <Muted>
                                This one-time bonus increases the organization's event limit. Current plan limit: <Number
                                    value={organization.max_events_per_month ?? 0}
                                />
                            </Muted>
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>

                <form.Field name="expires">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Expiration Date</Field.Label>
                            <Popover.Root bind:open={calendarOpen}>
                                <Popover.Trigger>
                                    {#snippet child({ props: triggerProps })}
                                        <Button {...triggerProps} variant="outline" class="w-full justify-start text-left font-normal" type="button">
                                            <CalendarIcon class="mr-2 size-4" />
                                            {field.state.value ? formatDateLabel(field.state.value) : 'Select a date...'}
                                        </Button>
                                    {/snippet}
                                </Popover.Trigger>
                                <Popover.Content class="w-auto p-0">
                                    <Calendar.Calendar
                                        value={calendarValue}
                                        onValueChange={handleCalendarChange}
                                        type="single"
                                        calendarLabel="Select an expiration date"
                                        locale="en-US"
                                    />
                                </Popover.Content>
                            </Popover.Root>
                            <Muted>Bonus events will expire after this date.</Muted>
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action type="submit" class={buttonVariants({ variant: 'default' })}>Set Bonus</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
