<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import Calendar from '@lucide/svelte/icons/calendar';

    import { getUtcMonthKey } from '../assistant-usage';

    let { time = $bindable() }: { time: string } = $props();
    let open = $state(false);
    const currentMonth = getUtcMonthKey();
    let month = $state(currentMonth);
    let selectedMonth = $state(currentMonth);
    const label = $derived(
        time === '[now-29d/d TO now]'
            ? 'Last 30 days'
            : !time
              ? 'Available history'
              : new Date(`${selectedMonth}-01T00:00:00Z`).toLocaleDateString(undefined, {
                    month: 'long',
                    timeZone: 'UTC',
                    year: 'numeric'
                })
    );

    function selectMonth(): void {
        selectedMonth = month;
        time = `[${month}-01||/M TO ${month}-01||+1M/M}`;
        open = false;
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="outline" class="w-44 justify-start" aria-label={`Usage period: ${label}`}>
                <Calendar data-icon="inline-start" />{label}
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="end" class="w-64">
        <Button
            class="mb-3 w-full"
            variant="ghost"
            onclick={() => {
                time = '[now-29d/d TO now]';
                open = false;
            }}>Last 30 days</Button
        >
        <form
            class="flex flex-col gap-3"
            onsubmit={(event) => {
                event.preventDefault();
                selectMonth();
            }}
        >
            <Field.FieldGroup>
                <Field.Field>
                    <Field.Label for="tour-usage-month">Month (UTC)</Field.Label>
                    <Input id="tour-usage-month" type="month" required max={currentMonth} bind:value={month} />
                </Field.Field>
            </Field.FieldGroup>
            <Button type="submit" variant="secondary">Show month</Button>
            <Button
                variant="ghost"
                onclick={() => {
                    time = '';
                    open = false;
                }}>Available history</Button
            >
        </form>
    </Popover.Content>
</Popover.Root>
