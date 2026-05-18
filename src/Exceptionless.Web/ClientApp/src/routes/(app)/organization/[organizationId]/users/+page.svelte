<script lang="ts">
    import type { ViewUser } from '$features/users/models';

    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { addOrganizationUser } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { type GetOrganizationUsersParams, getOrganizationUsersQuery } from '$features/users/api.svelte';
    import InviteUserDialog from '$features/users/components/invite-user-dialog.svelte';
    import { getTableOptions } from '$features/users/components/table/options.svelte';
    import UsersDataTable from '$features/users/components/table/users-data-table.svelte';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';

    const organizationId = organization.current!;

    const DEFAULT_PARAMS = {
        limit: DEFAULT_LIMIT
    };

    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const usersQueryParameters: GetOrganizationUsersParams = $state({
        get limit() {
            return queryParams.limit!;
        },
        set limit(value) {
            queryParams.limit = value;
        }
    });

    const usersQuery = getOrganizationUsersQuery({
        get params() {
            return usersQueryParameters;
        },
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const table = createTable(getTableOptions<ViewUser>(usersQueryParameters, usersQuery, organizationId));

    const addUserMutation = addOrganizationUser({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    $effect(() => {
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    let showInviteDialog = $state(false);
    let toastId = $state<number | string>();

    function handleInviteUser() {
        showInviteDialog = true;
    }

    async function inviteUser(email: string): Promise<void> {
        toast.dismiss(toastId);

        try {
            await addUserMutation.mutateAsync(email);
            toastId = toast.success('User invited successfully');
        } catch (error: unknown) {
            if (showBillingDialogOnUpgradeProblem(error, organizationId, () => inviteUser(email))) {
                return;
            }

            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to invite the user: ${message}`);
            throw error;
        }
    }
</script>

<div class="space-y-6">
    <Muted>Manage users for this organization</Muted>

    <UsersDataTable bind:limit={usersQueryParameters.limit!} isLoading={usersQuery.isLoading} {table}>
        {#snippet toolbarChildren()}
            <div class="flex-1"></div>
            <DataTableViewOptions size="icon-lg" {table} />
            <Button size="icon-lg" onclick={handleInviteUser} title="Invite User">
                <Plus class="size-4" aria-hidden="true" />
                <span class="sr-only">Invite User</span>
            </Button>
        {/snippet}
    </UsersDataTable>
</div>

<InviteUserDialog bind:open={showInviteDialog} {inviteUser} />
