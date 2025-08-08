<script lang="ts">
    import type { PostSetBonusOrganizationParams } from '$features/organizations/api.svelte';
    import type { ViewOrganization } from '$features/organizations/models';

    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as Calendar from '$comp/ui/calendar';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import { SetBonusOrganizationForm } from '$features/organizations/models';
    import Number from '$features/shared/components/formatters/number.svelte';
    import { formatDateLabel } from '$features/shared/dates';
    import { applyServerSideErrors } from "$features/shared/validation";
    import { ProblemDetails } from "@exceptionless/fetchclient";
    import { CalendarDate } from '@internationalized/date';
    import CalendarIcon from '@lucide/svelte/icons/calendar';
    import { SvelteDate } from 'svelte/reactivity';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    interface Props {
        open: boolean;
        organization: ViewOrganization;
        setBonus: (params: PostSetBonusOrganizationParams) => Promise<void>;
    }

    let { open = $bindable(), organization, setBonus }: Props = $props();

    const form = superForm(defaults(new SetBonusOrganizationForm(), classvalidatorClient(SetBonusOrganizationForm)), {
        dataType: 'json',
        id: 'set-bonus-organization-form',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            try {
                await setBonus({
                    bonusEvents: form.data.bonusEvents,
                    expires: form.data.expires,
                    organizationId: organization.id
                });

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
        validators: classvalidatorClient(SetBonusOrganizationForm)
    });

    const { enhance, form: formData } = form;

    let calendarValue = $state<CalendarDate | undefined>();
    let calendarOpen = $state(false);

    $effect(() => {
        if (open) {
            if (organization.bonus_events_per_month > 0) {
                $formData.bonusEvents = organization.bonus_events_per_month;
                if (organization.bonus_expiration) {
                    const expirationDate = new Date(organization.bonus_expiration);
                    $formData.expires = expirationDate;
                    calendarValue = new CalendarDate(expirationDate.getFullYear(), expirationDate.getMonth() + 1, expirationDate.getDate());
                }
            } else {
                // Set default values: 20% of plan limit and first day of next month
                const defaultBonus = Math.round(organization.max_events_per_month * 0.2);
                $formData.bonusEvents = defaultBonus;

                const nextMonth = new SvelteDate();
                nextMonth.setMonth(nextMonth.getMonth() + 1);
                nextMonth.setDate(1);
                nextMonth.setHours(0, 0, 0, 0);

                $formData.expires = nextMonth;
                calendarValue = new CalendarDate(nextMonth.getFullYear(), nextMonth.getMonth() + 1, nextMonth.getDate());
            }
        }
    });

    $effect(() => {
        if (calendarValue) {
            $formData.expires = new Date(Date.UTC(calendarValue.year, calendarValue.month - 1, calendarValue.day));
        }
    });
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <form method="POST" use:enhance>
            <AlertDialog.Header>
                <AlertDialog.Title>Set Bonus Events</AlertDialog.Title>
                <AlertDialog.Description>
                    Grant additional event capacity to the organization "{organization.name}".
                </AlertDialog.Description>
            </AlertDialog.Header>

            <P class="space-y-4 pb-4">
                <Form.Field {form} name="bonusEvents">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Bonus Events</Form.Label>
                            <Input
                                {...props}
                                type="number"
                                min="0"
                                step="1"
                                bind:value={$formData.bonusEvents}
                                placeholder="Enter number of bonus events to add"
                            />
                        {/snippet}
                    </Form.Control>
                    <Form.Description>
                        This one-time bonus increases the organization's event limit. Current plan limit: <Number value={organization.max_events_per_month} />
                    </Form.Description>
                    <Form.FieldErrors />
                </Form.Field>

                <Form.Field {form} name="expires">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Expiration Date</Form.Label>
                            <Popover.Root bind:open={calendarOpen}>
                                <Popover.Trigger>
                                    <Button {...props} variant="outline" class="w-full justify-start text-left font-normal" type="button">
                                        <CalendarIcon class="mr-2 size-4" />
                                        {$formData.expires ? formatDateLabel($formData.expires) : 'Select a date...'}
                                    </Button>
                                </Popover.Trigger>
                                <Popover.Content class="w-auto p-0">
                                    <Calendar.Calendar bind:value={calendarValue} type="single" calendarLabel="Select an expiration date" locale="en-US" />
                                </Popover.Content>
                            </Popover.Root>
                        {/snippet}
                    </Form.Control>
                    <Form.Description>Bonus events will expire after this date.</Form.Description>
                    <Form.FieldErrors />
                </Form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action type="submit" class={buttonVariants({ variant: 'default' })}>Set Bonus</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
