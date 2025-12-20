<script lang="ts">
    import type { ViewUser } from '$features/users/models';

    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
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
            const mutation = addOrganizationUser({
                route: {
                    email,
                    organizationId
                }
            });
            await mutation.mutateAsync();
            toastId = toast.success('User invited successfully');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to invite the user: ${message}`);
            throw error;
        }
    }
</script>

<div class="space-y-6">
    <div class="flex items-start justify-between">
        <div>
            <H3>Organization Users</H3>
            <Muted>Manage users for this organization.</Muted>
        </div>
        <Button size="icon" onclick={handleInviteUser} title="Invite User" class="shrink-0">
            <Plus class="size-4" aria-hidden="true" />
            <span class="sr-only">Invite User</span>
        </Button>
    </div>
    <Separator />

    <UsersDataTable bind:limit={usersQueryParameters.limit!} isLoading={usersQuery.isLoading} {table} />
</div>

<InviteUserDialog bind:open={showInviteDialog} {inviteUser} />
