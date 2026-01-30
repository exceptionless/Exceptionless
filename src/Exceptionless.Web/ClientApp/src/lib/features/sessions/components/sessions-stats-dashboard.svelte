<script lang="ts">
    import Duration from '$comp/formatters/duration.svelte';
    import Number from '$comp/formatters/number.svelte';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import AreaChart from '@lucide/svelte/icons/area-chart';
    import Clock from '@lucide/svelte/icons/clock';
    import LineChart from '@lucide/svelte/icons/trending-up';
    import Users from '@lucide/svelte/icons/users';

    interface Props {
        avgDuration?: number;
        avgPerHour?: number;
        isLoading?: boolean;
        totalSessions?: number;
        totalUsers?: number;
    }

    let { avgDuration = 0, avgPerHour = 0, isLoading = false, totalSessions = 0, totalUsers = 0 }: Props = $props();
</script>

<div class="grid grid-cols-2 gap-4 md:grid-cols-4">
    <Card.Root class="relative overflow-hidden">
        <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
            <Card.Title class="text-sm font-medium">Sessions</Card.Title>
            <AreaChart class="text-muted-foreground h-4 w-4" />
        </Card.Header>
        <Card.Content>
            {#if isLoading}
                <Skeleton class="h-8 w-24" />
            {:else}
                <div class="text-2xl font-bold">
                    <Number value={totalSessions} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root class="relative overflow-hidden">
        <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
            <Card.Title class="text-sm font-medium">Sessions Per Hour</Card.Title>
            <LineChart class="text-muted-foreground h-4 w-4" />
        </Card.Header>
        <Card.Content>
            {#if isLoading}
                <Skeleton class="h-8 w-24" />
            {:else}
                <div class="text-2xl font-bold">
                    {avgPerHour?.toFixed(1) ?? '0'}
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root class="relative overflow-hidden">
        <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
            <Card.Title class="text-sm font-medium">Users</Card.Title>
            <Users class="text-muted-foreground h-4 w-4" />
        </Card.Header>
        <Card.Content>
            {#if isLoading}
                <Skeleton class="h-8 w-24" />
            {:else}
                <div class="text-2xl font-bold">
                    <Number value={totalUsers} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root class="relative overflow-hidden">
        <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
            <Card.Title class="text-sm font-medium">Average Duration</Card.Title>
            <Clock class="text-muted-foreground h-4 w-4" />
        </Card.Header>
        <Card.Content>
            {#if isLoading}
                <Skeleton class="h-8 w-24" />
            {:else}
                <div class="text-2xl font-bold">
                    <!-- avgDuration is in seconds, Duration expects milliseconds -->
                    <Duration value={avgDuration * 1000} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>
</div>
