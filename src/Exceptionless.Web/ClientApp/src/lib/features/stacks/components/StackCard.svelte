<script lang="ts">
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import DateTime from "$comp/formatters/DateTime.svelte";
    import { P } from '$comp/typography';
    import * as Card from '$comp/ui/card';
    import { getStackByIdQuery } from '$features/stacks/api.svelte';
    import ChartLineVariant from '~icons/mdi/chart-line-variant';
    import InformationOutline from '~icons/mdi/information-outline';
    import Users from '~icons/mdi/users';

    import StackStatusDropdown from "./StackStatusDropdown.svelte";

    interface Props {
        id: string;
    }

    let { id }: Props = $props();

    let stackResponse = getStackByIdQuery({
        get id() {
            return id;
        }
    });

    const stack = $derived(stackResponse.data!);
</script>

{#if stackResponse.isLoading}
    <P>Loading...</P>
{:else if stackResponse.isSuccess}
    <div class="space-y-4">
        <div class="grid gap-4 sm:grid-cols-2 md:grid-cols-2 lg:grid-cols-4">
            <Card.Root>
                <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                    <Card.Title class="text-sm font-medium">Status</Card.Title>
                    <InformationOutline class="size-4 text-muted-foreground" />
                </Card.Header>
                <Card.Content>
                    <div class="text-2xl font-bold"><StackStatusDropdown value={stack.status} /></div>
                    <p class="text-xs text-muted-foreground">
                        {#if stack.date_fixed}
                            Fixed {#if stack.fixed_in_version}in {stack.fixed_in_version} {/if} on <DateTime value={stack.date_fixed}></DateTime>
                        {/if}
                        {#if stack.snooze_until_utc}
                            Snoozed until <DateTime value={stack.snooze_until_utc}></DateTime>
                        {/if}
                </Card.Content>
            </Card.Root>
            <Card.Root>
                <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                    <Card.Title class="text-sm font-medium">Users</Card.Title>
                    <Users class="size-4 text-muted-foreground" />
                </Card.Header>
                <Card.Content>
                    <div class="text-2xl font-bold">+2350</div>
                    <p class="text-xs text-muted-foreground">+180.1% from last month</p>
                </Card.Content>
            </Card.Root>
            <Card.Root>
                <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                    <Card.Title class="text-sm font-medium">Events / All time</Card.Title>
                    <ChartLineVariant class="size-4 text-muted-foreground" />
                </Card.Header>
                <Card.Content>
                    <div class="text-2xl font-bold">+{stack.total_occurrences} (First / Last)</div>
                    <p class="text-xs text-muted-foreground">+201 since last hour</p>
                </Card.Content>
            </Card.Root>
        </div>
    </div>
{:else}
    <ErrorMessage message={stackResponse.error?.errors.general}></ErrorMessage>
{/if}
