<script lang="ts">
    import Button from '$comp/ui/button/button.svelte';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import ChevronDown from '~icons/mdi/chevron-down';

    import { StackStatus } from '../models';

    interface Props {
        id: string;
        status: StackStatus;
    }

    let { id, status }: Props = $props();

    type Item = { label: string; value: StackStatus };
    const items: Item[] = [
        { label: 'Open', value: StackStatus.Open },
        { label: 'Fixed', value: StackStatus.Fixed },
        { label: 'Regressed', value: StackStatus.Regressed },
        { label: 'Snoozed', value: StackStatus.Snoozed },
        { label: 'Ignored', value: StackStatus.Ignored },
        { label: 'Discarded', value: StackStatus.Discarded }
    ];

    let selected = $derived((items.find((item) => item.value === status) || items[items.length - 1]) as Item);

    function updateOpen() {
        //stackService.changeStatus(id, "open")
    }

    function updateFixed() {
        // if (vm.stack.status === "fixed") {
        //                 return updateOpen();
        //             }
        // tackDialogService
        // .markFixed()
        //                 .then(function (version) {
        //                     return stackService
        //                         .markFixed(vm._stackId, version)
        //                         .then(onSuccess, onFailure)
        //                         .catch(function (e) {});
    }

    function updateSnooze(timePeriod?: '6hours' | 'day' | 'month' | 'week') {
        console.log(timePeriod, id);
        // if (!timePeriod && vm.stack.status === "snoozed") {
        //                 return updateOpen();
        //             }
        //
        //                     var snoozeUntilUtc = moment();
        //                     switch (timePeriod) {
        //                         case "6hours":
        //                             snoozeUntilUtc = snoozeUntilUtc.add(6, "hours");
        //                             break;
        //                         case "day":
        //                             snoozeUntilUtc = snoozeUntilUtc.add(1, "days");
        //                             break;
        //                         case "week":
        //                             snoozeUntilUtc = snoozeUntilUtc.add(1, "weeks");
        //                             break;
        //                         case "month":
        //                         default:
        //                             snoozeUntilUtc = snoozeUntilUtc.add(1, "months");
        //                             break;
        //                     }
        //
        //                     return stackService
        //                         .markSnoozed(vm._stackId, snoozeUntilUtc.format("YYYY-MM-DDTHH:mm:ssz"))
    }

    function updateIgnore() {
        // var ignored = vm.stack.status === "ignored";
        //             return stackService
        //                 .changeStatus(vm._stackId, ignored ? "open" : "ignored")
    }

    function updateDiscard() {
        // if (vm.stack.status === "discarded") {
        //                 return updateOpen();
        //             }
        // translateService.T(
        //                     "Are you sure you want to all current stack events and discard any future stack events?"
        //                 ) +
        //                 " " +
        //                 translateService.T(
        //                     "All future occurrences will be discarded and will not count against your event limit."
        //                 );
        //changeStatus(vm._stackId, "discarded")
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        <Button variant="outline">
            {selected.label}
            <ChevronDown class="size-4" />
        </Button>
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Group>
            <DropdownMenu.GroupHeading>Update Status</DropdownMenu.GroupHeading>
            <DropdownMenu.Separator />
            <DropdownMenu.Item title="Mark this stack as open" onchange={() => updateOpen()}>Open</DropdownMenu.Item>
            <DropdownMenu.Item title="Mark this stack as fixed" onchange={() => updateFixed()}>Fixed</DropdownMenu.Item>
            <DropdownMenu.Sub>
                <DropdownMenu.SubTrigger title="Hide this stack from reports and mutes occurrence notifications" onchange={() => updateSnooze()}
                    >Snoozed</DropdownMenu.SubTrigger
                >
                <DropdownMenu.SubContent>
                    <DropdownMenu.Item onchange={() => updateSnooze('6hours')}>6 Hours</DropdownMenu.Item>
                    <DropdownMenu.Item onchange={() => updateSnooze('day')}>1 Day</DropdownMenu.Item>
                    <DropdownMenu.Item onchange={() => updateSnooze('week')}>1 Week</DropdownMenu.Item>
                    <DropdownMenu.Item onchange={() => updateSnooze('month')}>1 Month</DropdownMenu.Item>
                </DropdownMenu.SubContent>
            </DropdownMenu.Sub>
            <DropdownMenu.Item title="Stop sending occurrence notifications for this stack" onchange={() => updateIgnore()}>Ignored</DropdownMenu.Item>
            <DropdownMenu.Item title="All future occurrences will be discarded and will not count against your event limit" onchange={() => updateDiscard()}
                >Discarded</DropdownMenu.Item
            >
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>
