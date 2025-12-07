<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';

    import PageNumber from '$comp/page-number.svelte';
    import { Muted, P } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Pagination, PaginationContent, PaginationFirstButton, PaginationItem, PaginationNextButton, PaginationPrevButton } from '$comp/ui/pagination';
    import * as Select from '$comp/ui/select';
    import { Skeleton } from '$comp/ui/skeleton';
    import { getAdminOrganizationsQuery } from '$features/organizations/api.svelte';
    import SuspensionIndicator from '$features/organizations/components/suspension-indicator.svelte';
    import { getSuspensionLabel } from '$features/organizations/suspension-utils';
    import Number from '$features/shared/components/formatters/number.svelte';
    import TimeAgo from '$features/shared/components/formatters/time-ago.svelte';
    import Notification from '$features/shared/components/notification/notification.svelte';
    import SearchInput from '$features/shared/components/search-input.svelte';
    import { BillingStatus } from '$generated/api';
    import { getInitials } from '$shared/strings';
    import Activity from '@lucide/svelte/icons/activity';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Ban from '@lucide/svelte/icons/ban';
    import Bug from '@lucide/svelte/icons/bug';
    import Calendar from '@lucide/svelte/icons/calendar';
    import CalendarCheck from '@lucide/svelte/icons/calendar-check';
    import CalendarDays from '@lucide/svelte/icons/calendar-days';
    import Clock from '@lucide/svelte/icons/clock';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import Folder from '@lucide/svelte/icons/folder';
    import Gauge from '@lucide/svelte/icons/gauge';
    import Gift from '@lucide/svelte/icons/gift';
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';
    import UserRoundSearch from '@lucide/svelte/icons/user-round-search';

    interface Props {
        impersonateOrganization: (organization: ViewOrganization) => Promise<void>;
        open: boolean;
        userOrganizationIds?: string[];
    }

    let { impersonateOrganization, open = $bindable(), userOrganizationIds = [] }: Props = $props();

    let searchQuery = $state('');
    let paidFilter = $state<boolean | undefined>(undefined);
    let suspendedFilter = $state<boolean | undefined>(undefined);
    let selectedOrganization = $state<null | ViewOrganization>(null);
    let currentPage = $state(1);
    const pageSize = 5;

    const searchResults = getAdminOrganizationsQuery({
        params: {
            get criteria() {
                return searchQuery || undefined;
            },
            limit: pageSize,
            get page() {
                return currentPage;
            },
            get paid() {
                return paidFilter;
            },
            get suspended() {
                return suspendedFilter;
            }
        }
    });

    const organizations = $derived(searchResults.data?.data ?? []);
    const totalCount = $derived((searchResults.data?.meta?.total as number) ?? organizations.length);
    const totalPages = $derived(Math.max(1, Math.ceil(totalCount / pageSize)));
    const hasFilters = $derived(searchQuery !== '' || paidFilter !== undefined || suspendedFilter !== undefined);

    function resetFilters() {
        searchQuery = '';
        paidFilter = undefined;
        suspendedFilter = undefined;
        currentPage = 1;
        selectedOrganization = null;
    }

    function isUserMember(orgId: string | undefined): boolean {
        return orgId ? userOrganizationIds.includes(orgId) : false;
    }

    const actionButtonText = $derived(selectedOrganization && isUserMember(selectedOrganization.id) ? 'View Organization' : 'Impersonate');
    function handleSelect(organization: ViewOrganization) {
        selectedOrganization = organization;
    }

    async function handleImpersonate() {
        if (selectedOrganization) {
            await impersonateOrganization(selectedOrganization);
            open = false;
            searchQuery = '';
            selectedOrganization = null;
            currentPage = 1;
        }
    }

    function handleCancel() {
        open = false;
        searchQuery = '';
        selectedOrganization = null;
        currentPage = 1;
    }

    function handleOpenChange(isOpen: boolean) {
        open = isOpen;
        if (!isOpen) {
            searchQuery = '';
            paidFilter = undefined;
            suspendedFilter = undefined;
            selectedOrganization = null;
            currentPage = 1;
        }
    }

    function handlePageChange(page: number) {
        currentPage = page;
        selectedOrganization = null;
    }

    $effect(() => {
        void searchQuery;
        void paidFilter;
        void suspendedFilter;
        currentPage = 1;
        selectedOrganization = null;
    });

    function getBillingStatusLabel(status: BillingStatus | null | undefined): string {
        switch (status) {
            case BillingStatus.Active:
                return 'Active';
            case BillingStatus.Canceled:
                return 'Canceled';
            case BillingStatus.PastDue:
                return 'Past Due';
            case BillingStatus.Trialing:
                return 'Trialing';
            case BillingStatus.Unpaid:
                return 'Unpaid';
            default:
                return 'Unknown';
        }
    }

    function getBillingStatusColor(status: BillingStatus | null | undefined): string {
        switch (status) {
            case BillingStatus.Active:
                return 'text-green-600 dark:text-green-400';
            case BillingStatus.Canceled:
                return 'text-gray-500 dark:text-gray-400';
            case BillingStatus.PastDue:
                return 'text-orange-600 dark:text-orange-400';
            case BillingStatus.Trialing:
                return 'text-blue-600 dark:text-blue-400';
            case BillingStatus.Unpaid:
                return 'text-red-600 dark:text-red-400';
            default:
                return 'text-muted-foreground';
        }
    }

    function getLastEventDate(org: ViewOrganization): null | string {
        // Find the most recent hour with events from usage_hours
        if (org.usage_hours && org.usage_hours.length > 0) {
            const hoursWithEvents = org.usage_hours.filter((h) => h.total > 0).sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
            if (hoursWithEvents.length > 0) {
                return hoursWithEvents[0]!.date;
            }
        }
        // Fall back to monthly usage if no hourly data
        if (org.usage && org.usage.length > 0) {
            const monthsWithEvents = org.usage.filter((u) => u.total > 0).sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
            if (monthsWithEvents.length > 0) {
                return monthsWithEvents[0]!.date;
            }
        }
        return null;
    }
</script>

<Dialog.Root bind:open onOpenChange={handleOpenChange}>
    <Dialog.Content class="max-h-[90vh] sm:max-w-lg">
        <Dialog.Header>
            <Dialog.Title>Impersonate Organization</Dialog.Title>
            <Dialog.Description>Select an organization to view as an admin.</Dialog.Description>
        </Dialog.Header>

        <div class="space-y-3 py-2">
            <SearchInput bind:value={searchQuery} placeholder="Search by name or Id..." />

            <div class="flex flex-wrap items-center gap-2">
                <Select.Root
                    type="single"
                    value={paidFilter === undefined ? 'all' : paidFilter ? 'paid' : 'free'}
                    onValueChange={(value) => {
                        if (value === 'all') {
                            paidFilter = undefined;
                        } else {
                            paidFilter = value === 'paid';
                        }
                    }}
                >
                    <Select.Trigger class="h-8 w-[120px]">
                        {#if paidFilter === undefined}
                            All Plans
                        {:else if paidFilter}
                            Paid
                        {:else}
                            Free
                        {/if}
                    </Select.Trigger>
                    <Select.Content>
                        <Select.Item value="all">All Plans</Select.Item>
                        <Select.Item value="paid">Paid</Select.Item>
                        <Select.Item value="free">Free</Select.Item>
                    </Select.Content>
                </Select.Root>

                <Select.Root
                    type="single"
                    value={suspendedFilter === undefined ? 'all' : suspendedFilter ? 'suspended' : 'active'}
                    onValueChange={(value) => {
                        if (value === 'all') {
                            suspendedFilter = undefined;
                        } else {
                            suspendedFilter = value === 'suspended';
                        }
                    }}
                >
                    <Select.Trigger class="h-8 w-[120px]">
                        {#if suspendedFilter === undefined}
                            All Status
                        {:else if suspendedFilter}
                            Suspended
                        {:else}
                            Active
                        {/if}
                    </Select.Trigger>
                    <Select.Content>
                        <Select.Item value="all">All Status</Select.Item>
                        <Select.Item value="active">Active</Select.Item>
                        <Select.Item value="suspended">Suspended</Select.Item>
                    </Select.Content>
                </Select.Root>

                {#if hasFilters}
                    <Button variant="ghost" size="sm" onclick={resetFilters} class="h-8 text-xs">Reset filters</Button>
                {/if}
            </div>

            <div class="h-[45vh] min-h-[250px] overflow-y-auto rounded-md border">
                <div class="p-1.5">
                    {#if searchResults.isFetching}
                        <div class="space-y-1">
                            {#each [1, 2, 3, 4, 5] as i (i)}
                                <div class="flex items-center gap-2.5 rounded-md p-2">
                                    <Skeleton class="size-9 rounded-lg" />
                                    <div class="flex-1 space-y-1">
                                        <Skeleton class="h-4 w-36" />
                                        <Skeleton class="h-3 w-28" />
                                    </div>
                                </div>
                            {/each}
                        </div>
                    {:else if organizations.length > 0}
                        <div class="space-y-0.5">
                            {#each organizations as organization (organization.id)}
                                {@const isSelected = selectedOrganization?.id === organization.id}
                                {@const isMember = isUserMember(organization.id)}
                                <div class="rounded-md {isSelected ? 'ring-primary bg-accent ring-1' : ''}">
                                    <button
                                        type="button"
                                        onclick={() => handleSelect(organization)}
                                        class="hover:bg-accent hover:text-accent-foreground flex w-full items-center gap-2.5 rounded-md p-2 text-left transition-colors"
                                    >
                                        <Avatar.Root class="size-9 shrink-0 rounded-lg border">
                                            <Avatar.Fallback class="rounded-lg text-xs">
                                                {getInitials(organization.name)}
                                            </Avatar.Fallback>
                                        </Avatar.Root>
                                        <div class="min-w-0 flex-1">
                                            <div class="flex items-center gap-1.5">
                                                <P class="truncate text-sm font-medium">{organization.name}</P>
                                                {#if organization.is_suspended}
                                                    <SuspensionIndicator code={organization.suspension_code} notes={organization.suspension_notes} />
                                                {:else}
                                                    <Badge class="shrink-0 bg-green-100 text-[10px] text-green-700 dark:bg-green-900/30 dark:text-green-300"
                                                        >Active</Badge
                                                    >
                                                {/if}
                                                {#if isMember}
                                                    <Badge class="shrink-0 bg-blue-100 text-[10px] text-blue-700 dark:bg-blue-900/30 dark:text-blue-300"
                                                        >Member</Badge
                                                    >
                                                {/if}
                                            </div>
                                            <Muted class="flex items-center gap-1 font-mono text-[11px]">
                                                <span class="truncate">{organization.id}</span>
                                                <span class="text-muted-foreground/50">•</span>
                                                <span>{organization.plan_name}</span>
                                            </Muted>
                                        </div>
                                    </button>

                                    {#if isSelected}
                                        <div class="space-y-2 border-t px-2.5 py-2 text-xs">
                                            {#if organization.is_suspended}
                                                <Notification variant="destructive" class="px-2 py-1.5 text-xs">
                                                    {#snippet icon()}<Ban class="size-3.5" />{/snippet}
                                                    <span>
                                                        <span class="font-medium">Suspended</span>
                                                        {#if organization.suspension_date}<TimeAgo value={organization.suspension_date} />{/if}
                                                        for
                                                        <span class="font-medium">{getSuspensionLabel(organization.suspension_code)}</span
                                                        >{#if organization.suspension_notes}: {organization.suspension_notes}{/if}
                                                    </span>
                                                </Notification>
                                            {/if}

                                            {#if organization.is_over_monthly_limit || organization.is_over_request_limit}
                                                <Notification variant="warning" class="px-2 py-1.5 text-xs">
                                                    {#snippet icon()}<AlertTriangle class="size-3.5" />{/snippet}
                                                    <span>
                                                        <span class="font-medium">Over Limit:</span>
                                                        {#if organization.is_over_monthly_limit}Monthly{/if}
                                                        {#if organization.is_over_request_limit}{organization.is_over_monthly_limit ? ', ' : ''}Request{/if}
                                                    </span>
                                                </Notification>
                                            {/if}

                                            <div class="bg-muted/50 space-y-2.5 rounded-md px-2 py-2.5">
                                                <div class="flex items-center justify-between">
                                                    <div class="flex items-center gap-1.5">
                                                        <Muted class="flex items-center gap-1.5"><CreditCard class="size-3.5" />Plan:</Muted>
                                                        <span class="font-medium">{organization.plan_name}</span>
                                                        <span class={getBillingStatusColor(organization.billing_status)}>
                                                            {getBillingStatusLabel(organization.billing_status)}
                                                        </span>
                                                    </div>
                                                    {#if organization.billing_change_date}
                                                        <Muted>Changed <TimeAgo value={organization.billing_change_date} /></Muted>
                                                    {/if}
                                                </div>
                                                {#if organization.subscribe_date}
                                                    <div class="flex items-center justify-between">
                                                        <div class="flex items-center gap-1.5">
                                                            <Muted class="flex items-center gap-1.5"><CalendarCheck class="size-3.5" />Subscribed:</Muted>
                                                            <span class="font-medium"><TimeAgo value={organization.subscribe_date} /></span>
                                                        </div>
                                                    </div>
                                                {/if}
                                                <div class="flex items-center justify-between">
                                                    <div class="flex items-center gap-1.5">
                                                        <Muted class="flex items-center gap-1.5"><Gauge class="size-3.5" />Limit:</Muted>
                                                        <span class="font-medium"><Number value={organization.max_events_per_month} /> events/mo</span>
                                                    </div>
                                                </div>
                                                <div class="flex items-center justify-between">
                                                    <div class="flex items-center gap-1.5">
                                                        <Muted class="flex items-center gap-1.5"><Clock class="size-3.5" />Retention:</Muted>
                                                        <span class="font-medium"><Number value={organization.retention_days} /> days</span>
                                                    </div>
                                                </div>
                                                {#if organization.bonus_events_per_month && organization.bonus_events_per_month > 0}
                                                    <div class="flex items-center justify-between">
                                                        <div class="flex items-center gap-1.5">
                                                            <Muted class="flex items-center gap-1.5"><Gift class="size-3.5" />Bonus:</Muted>
                                                            <span class="font-medium"><Number value={organization.bonus_events_per_month} /> events/mo</span>
                                                        </div>
                                                        {#if organization.bonus_expiration}
                                                            <Muted>Expires <TimeAgo value={organization.bonus_expiration} /></Muted>
                                                        {/if}
                                                    </div>
                                                {/if}
                                            </div>

                                            <div class="space-y-2 px-2">
                                                <div class="flex gap-3">
                                                    <Muted class="flex items-center gap-1.5">
                                                        <Folder class="size-3.5" />
                                                        <span class="text-foreground font-medium"><Number value={organization.project_count ?? 0} /></span>
                                                        projects
                                                    </Muted>
                                                    <Muted class="flex items-center gap-1.5">
                                                        <Bug class="size-3.5" />
                                                        <span class="text-foreground font-medium"><Number value={organization.stack_count ?? 0} /></span>
                                                        stacks
                                                    </Muted>
                                                    <Muted class="flex items-center gap-1.5">
                                                        <CalendarDays class="size-3.5" />
                                                        <span class="text-foreground font-medium"><Number value={organization.event_count ?? 0} /></span>
                                                        events
                                                    </Muted>
                                                </div>
                                                {#if organization.created_utc}
                                                    <Muted class="flex items-center gap-1.5">
                                                        <Calendar class="size-3.5" />
                                                        Created
                                                        <span class="text-foreground font-medium"><TimeAgo value={organization.created_utc} /></span>
                                                    </Muted>
                                                {/if}
                                                {#if organization.updated_utc && organization.created_utc !== organization.updated_utc}
                                                    <Muted class="flex items-center gap-1.5">
                                                        <RefreshCw class="size-3.5" />
                                                        Updated
                                                        <span class="text-foreground font-medium"><TimeAgo value={organization.updated_utc} /></span>
                                                    </Muted>
                                                {/if}
                                                {#if getLastEventDate(organization)}
                                                    <Muted class="flex items-center gap-1.5">
                                                        <Activity class="size-3.5" />
                                                        Last Event
                                                        <span class="text-foreground font-medium"><TimeAgo value={getLastEventDate(organization)!} /></span>
                                                    </Muted>
                                                {/if}
                                            </div>
                                        </div>
                                    {/if}
                                </div>
                            {/each}
                        </div>
                    {:else}
                        <div class="flex flex-col items-center justify-center py-8 text-center">
                            <UserRoundSearch class="text-muted-foreground mb-2 size-8" />
                            <P class="text-muted-foreground text-sm font-medium">No organizations found</P>
                            <Muted class="text-xs">
                                {#if hasFilters}
                                    Try adjusting your search or <button type="button" class="text-primary hover:underline" onclick={resetFilters}
                                        >reset filters</button
                                    >
                                {:else}
                                    Try a different search query
                                {/if}
                            </Muted>
                        </div>
                    {/if}
                </div>
            </div>

            <div class="flex items-center justify-between gap-3 text-xs sm:flex-nowrap">
                <div class="flex items-center gap-2">
                    <PageNumber {currentPage} size="xs" {totalPages} />
                    {#if totalCount > 0}
                        <Muted class="text-[11px]">{totalCount} organizations</Muted>
                    {/if}
                </div>
                {#if totalPages > 1}
                    <Pagination
                        count={totalCount}
                        bind:page={currentPage}
                        class="mx-0 w-auto justify-start"
                        onPageChange={handlePageChange}
                        perPage={pageSize}
                        siblingCount={0}
                    >
                        <PaginationContent class="gap-1">
                            <PaginationItem>
                                <PaginationFirstButton {currentPage} />
                            </PaginationItem>
                            <PaginationItem>
                                <PaginationPrevButton />
                            </PaginationItem>
                            <PaginationItem>
                                <PaginationNextButton />
                            </PaginationItem>
                        </PaginationContent>
                    </Pagination>
                {/if}
            </div>
        </div>

        <Dialog.Footer>
            <Button variant="outline" onclick={handleCancel}>Cancel</Button>
            <Button onclick={handleImpersonate} disabled={!selectedOrganization}>{actionButtonText}</Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
