<script lang="ts">
    import type { IFilter } from '$comp/filters/filters.svelte';

    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import DateTime from '$comp/formatters/DateTime.svelte';
    import Number from '$comp/formatters/Number.svelte';
    import TimeAgo from '$comp/formatters/TimeAgo.svelte';
    import Muted from '$comp/typography/Muted.svelte';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Tooltip from '$comp/ui/tooltip';
    import { getStackByIdQuery } from '$features/stacks/api.svelte';
    import IconFirstOccurrence from '~icons/mdi/arrow-left-circle';
    import IconLastOccurrence from '~icons/mdi/arrow-right-circle';
    import IconCalendar from '~icons/mdi/calendar';
    import IconClock from '~icons/mdi/clock';
    import IconFilter from '~icons/mdi/filter';
    import IconReference from '~icons/mdi/link';
    import IconOpenInNew from '~icons/mdi/open-in-new';
    import IconUsers from '~icons/mdi/users';

    import StackOptionsDropdownMenu from './StackOptionsDropdownMenu.svelte';
    import StackStatusDropdownMenu from './StackStatusDropdownMenu.svelte';

    interface Props {
        changed: (filter: IFilter) => void;
        id: string;
    }

    let { changed, id }: Props = $props();

    let stackResponse = getStackByIdQuery({
        get id() {
            return id;
        }
    });

    const stack = $derived(stackResponse.data!);
</script>

{#if stack}
    <Card.Root>
        <Card.Header>
            <Card.Title class="flex flex-row items-center justify-between text-lg font-semibold">
                <span class="mb-2 flex flex-col lg:mb-0">
                    <div class="flex items-center">
                        <ClickableStringFilter {changed} class="mr-2" term="stack" value={stack.id} />
                        <span class="truncate">{stack.title}</span>
                    </div>
                </span>
                <div class="flex items-center space-x-2">
                    <StackStatusDropdownMenu {stack} />
                    <StackOptionsDropdownMenu {stack} />
                </div>
            </Card.Title>
        </Card.Header>
        <Card.Content class="space-y-4 pt-2">
            <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconCalendar class="mb-1 size-6 text-primary" />
                        <span class="text-lg font-bold"><Number value={stack.total_occurrences} /></span>
                        <Muted>Total Events</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        <Number value={stack.total_occurrences} /> All Time
                    </Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconUsers class="mb-1 size-6 text-primary" />
                        <span class="text-lg font-bold"><Number value={12345} /></span>
                        <Muted>Users Affected</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">Users Affected</Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconFirstOccurrence class="mb-1 size-6 text-muted-foreground" />
                        <span class="text-lg font-bold"><TimeAgo value={stack.first_occurrence} /></span>
                        <Muted>First</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        First Occurred On <DateTime value={stack.first_occurrence} />
                    </Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconLastOccurrence class="mb-1 size-6 text-muted-foreground" />
                        <span class="text-lg font-bold"><TimeAgo value={stack.last_occurrence} /></span>
                        <Muted>Last</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        Last Occurred On <DateTime value={stack.last_occurrence} />
                    </Tooltip.Content>
                </Tooltip.Root>
            </div>

            <!-- Line Chart -->

            <div class="grid grid-cols-1 gap-4 lg:grid-cols-2">
                {#if (stack.status === 'fixed' || stack.status === 'regressed') && stack.date_fixed}
                    <div class="flex items-center gap-2 text-sm">
                        <IconCalendar class="size-4 text-green-500" />
                        <span>
                            Fixed {stack.fixed_in_version && `in ${stack.fixed_in_version}`} on <DateTime value={stack.date_fixed} />
                        </span>
                    </div>
                {/if}

                {#if stack.status === 'snoozed' && stack.snooze_until_utc}
                    <div class="flex items-center gap-2 text-sm">
                        <IconClock class="size-4 text-blue-500" />
                        <span>Snoozed until <DateTime value={stack.snooze_until_utc} /></span>
                    </div>
                {/if}
            </div>

            {#if stack.tags && stack.tags.length > 0}
                <div class="flex flex-wrap gap-2">
                    {#each stack.tags as tag (tag)}
                        <Badge color="dark"
                            ><ClickableStringFilter {changed} class="mr-1" term="tag" value={tag}
                                ><IconFilter class="text-muted-foreground text-opacity-80 hover:text-secondary" /></ClickableStringFilter
                            >{tag}</Badge
                        >
                    {/each}
                </div>
            {/if}

            <!-- TODO: Move to stack grid? -->
            {#if stack.references && stack.references.length > 0}
                <div class="flex items-center gap-2 text-sm">
                    <span class="text-blue-500" title="Reference Links"> <IconReference class="size-4" /></span>
                    {#each stack.references as referenceUrl}
                        <Button href={referenceUrl} rel="noopener noreferrer" size="sm" target="_blank" title="Open in new window" variant="ghost"
                            >{referenceUrl} <IconOpenInNew /></Button
                        >
                    {/each}
                </div>
            {/if}
        </Card.Content>
    </Card.Root>
{/if}
