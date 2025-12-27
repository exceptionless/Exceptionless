<script lang="ts">
    import type { DropdownItem } from '$features/shared/options';

    import Button from '$comp/ui/button/button.svelte';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import { SvelteDate } from 'svelte/reactivity';

    import type { Stack } from '../models';

    import { postChangeStatus, postMarkFixed, postMarkSnoozed } from '../api.svelte';
    import { StackStatus } from '../models';
    import { stackStatuses } from '../options';
    import MarkStackDiscardedDialog from './dialogs/mark-stack-discarded-dialog.svelte';
    import MarkStackFixedInVersionDialog from './dialogs/mark-stack-fixed-in-version-dialog.svelte';

    interface Props {
        stack: Stack;
    }

    let { stack }: Props = $props();

    let openMarkStackDiscardedDialog = $state<boolean>(false);
    let openMarkStackFixedInVersionDialog = $state<boolean>(false);
    let selected = $derived(
        (stackStatuses.find((option) => option.value === stack?.status) || stackStatuses[stackStatuses.length - 1]) as DropdownItem<StackStatus>
    );

    const updateMarkFixed = postMarkFixed({
        route: {
            get ids() {
                return [stack?.id].filter(Boolean);
            }
        }
    });

    const updateMarkSnoozed = postMarkSnoozed({
        route: {
            get ids() {
                return [stack?.id].filter(Boolean);
            }
        }
    });

    const changeStatus = postChangeStatus({
        route: {
            get ids() {
                return [stack?.id].filter(Boolean);
            }
        }
    });

    async function markOpen() {
        if (stack.status === StackStatus.Open) {
            return;
        }

        await changeStatus.mutateAsync(StackStatus.Open);
    }

    async function markFixed(version?: string) {
        await updateMarkFixed.mutateAsync(version);
    }

    async function markSnoozed(timePeriod?: '6hours' | 'day' | 'month' | 'week') {
        let snoozeUntilUtc = new SvelteDate();
        switch (timePeriod) {
            case '6hours':
                snoozeUntilUtc.setHours(snoozeUntilUtc.getHours() + 6);
                break;
            case 'day':
                snoozeUntilUtc.setDate(snoozeUntilUtc.getDate() + 1);
                break;
            case 'week':
                snoozeUntilUtc.setDate(snoozeUntilUtc.getDate() + 7);
                break;
            case 'month':
            default:
                snoozeUntilUtc.setMonth(snoozeUntilUtc.getMonth() + 1);
                break;
        }

        await updateMarkSnoozed.mutateAsync(snoozeUntilUtc);
    }

    async function markIgnored() {
        if (stack.status === StackStatus.Ignored) {
            return;
        }

        await changeStatus.mutateAsync(StackStatus.Ignored);
    }

    async function markDiscarded() {
        if (stack.status === StackStatus.Discarded) {
            return;
        }

        await changeStatus.mutateAsync(StackStatus.Discarded);
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="outline">
                {selected.label}
                <ChevronDown class="size-4" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Group>
            <DropdownMenu.GroupHeading>Update Status</DropdownMenu.GroupHeading>
            <DropdownMenu.Separator />
            <DropdownMenu.Item title="Mark this stack as open" onclick={() => markOpen()}>Open</DropdownMenu.Item>
            <DropdownMenu.Item title="Mark this stack as fixed" onclick={() => (openMarkStackFixedInVersionDialog = true)}>Fixed</DropdownMenu.Item>
            <DropdownMenu.Sub>
                <DropdownMenu.SubTrigger title="Hide this stack from reports and mutes occurrence notifications" onclick={() => markSnoozed()}
                    >Snoozed</DropdownMenu.SubTrigger
                >
                <DropdownMenu.SubContent>
                    <DropdownMenu.Item onclick={() => markSnoozed('6hours')}>6 Hours</DropdownMenu.Item>
                    <DropdownMenu.Item onclick={() => markSnoozed('day')}>1 Day</DropdownMenu.Item>
                    <DropdownMenu.Item onclick={() => markSnoozed('week')}>1 Week</DropdownMenu.Item>
                    <DropdownMenu.Item onclick={() => markSnoozed('month')}>1 Month</DropdownMenu.Item>
                </DropdownMenu.SubContent>
            </DropdownMenu.Sub>
            <DropdownMenu.Item title="Stop sending occurrence notifications for this stack" onclick={() => markIgnored()}>Ignored</DropdownMenu.Item>
            <DropdownMenu.Item
                title="All future occurrences will be discarded and will not count against your event limit"
                onclick={() => (openMarkStackDiscardedDialog = true)}>Discarded</DropdownMenu.Item
            >
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if openMarkStackDiscardedDialog}
    <MarkStackDiscardedDialog bind:open={openMarkStackDiscardedDialog} discard={markDiscarded} />
{/if}
{#if openMarkStackFixedInVersionDialog}
    <MarkStackFixedInVersionDialog bind:open={openMarkStackFixedInVersionDialog} save={markFixed} />
{/if}
