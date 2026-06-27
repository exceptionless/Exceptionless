<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import DateTime from '$comp/formatters/date-time.svelte';
    import { Muted, P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Badge } from '$comp/ui/badge';
    import { Button, buttonVariants } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { Spinner } from '$comp/ui/spinner';
    import * as Table from '$comp/ui/table';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { deleteOAuthGrantMutation, getOAuthGrantsQuery } from '$features/users/api.svelte';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import { toast } from 'svelte-sonner';

    const grantsQuery = getOAuthGrantsQuery();
    const revokeGrant = deleteOAuthGrantMutation();
    const organizationsQuery = getOrganizationsQuery({});

    let revokeDialogOpen = $state(false);
    let selectedGrant = $state<null | OAuthGrant>(null);
    let toastId = $state<number | string>();

    const grants = $derived(grantsQuery.data ?? []);
    const organizations = $derived(organizationsQuery.data?.data ?? []);
    const organizationNamesById = $derived(new Map(organizations.map((organization) => [organization.id, organization.name])));

    function confirmRevoke(grant: OAuthGrant) {
        selectedGrant = grant;
        revokeDialogOpen = true;
    }

    async function revokeSelectedGrant() {
        if (!selectedGrant) {
            return;
        }

        toast.dismiss(toastId);
        try {
            await revokeGrant.mutateAsync(selectedGrant.id);
            toastId = toast.success('Application access revoked.');
            revokeDialogOpen = false;
            selectedGrant = null;
        } catch {
            toastId = toast.error('Error revoking application access. Please try again.');
        }
    }

    function formatOrganization(id: string) {
        return organizationNamesById.get(id) ?? id;
    }

    function formatResource(resource: string) {
        if (resource.endsWith('/mcp')) {
            return 'MCP';
        }

        if (resource.endsWith('/api/v2')) {
            return 'REST API';
        }

        return resource;
    }

    function formatScope(scope: string) {
        switch (scope) {
            case 'events:read':
                return 'Events';
            case 'mcp:read':
                return 'MCP';
            case 'offline_access':
                return 'Offline';
            case 'projects:read':
                return 'Projects';
            case 'stacks:read':
                return 'Stacks';
            case 'stacks:write':
                return 'Stacks Write';
            default:
                return scope;
        }
    }
</script>

<div class="space-y-6">
    <div>
        <Muted>Manage applications connected to your account</Muted>
    </div>
    <Separator />

    {#if grantsQuery.isPending}
        <div class="text-muted-foreground flex items-center gap-2 py-8 text-sm">
            <Spinner />
            Loading applications...
        </div>
    {:else if grantsQuery.isError}
        <P class="text-destructive py-8 text-sm">Failed to load applications.</P>
    {:else if grants.length === 0}
        <P class="text-muted-foreground py-8 text-sm">No applications have access to your account.</P>
    {:else}
        <div class="overflow-x-auto">
            <Table.Root>
                <Table.Header>
                    <Table.Row>
                        <Table.Head>Application</Table.Head>
                        <Table.Head>Access</Table.Head>
                        <Table.Head>Organizations</Table.Head>
                        <Table.Head>Updated</Table.Head>
                        <Table.Head class="w-16 text-right">Actions</Table.Head>
                    </Table.Row>
                </Table.Header>
                <Table.Body>
                    {#each grants as grant (grant.id)}
                        <Table.Row>
                            <Table.Cell>
                                <div class="font-medium">{grant.application_name}</div>
                                <div class="text-muted-foreground mt-1 max-w-80 truncate text-xs" title={grant.client_id}>{grant.client_id}</div>
                                {#if grant.is_application_disabled}
                                    <Badge variant="outline" class="mt-2">Disabled</Badge>
                                {/if}
                            </Table.Cell>
                            <Table.Cell>
                                <div class="space-y-2">
                                    {#each grant.resources as resource (resource.resource)}
                                        <div>
                                            <div class="text-sm font-medium">{formatResource(resource.resource)}</div>
                                            <div class="mt-1 flex max-w-72 flex-wrap gap-1">
                                                {#each resource.scopes as scope (scope)}
                                                    <Badge variant={scope === 'stacks:write' ? 'amber' : 'secondary'}>{formatScope(scope)}</Badge>
                                                {/each}
                                            </div>
                                        </div>
                                    {/each}
                                </div>
                            </Table.Cell>
                            <Table.Cell>
                                <div class="flex max-w-72 flex-wrap gap-1">
                                    {#each grant.organization_ids as organizationId (organizationId)}
                                        <Badge variant="outline" title={organizationId}>{formatOrganization(organizationId)}</Badge>
                                    {/each}
                                </div>
                            </Table.Cell>
                            <Table.Cell class="text-sm whitespace-nowrap">
                                <DateTime value={grant.updated_utc} />
                            </Table.Cell>
                            <Table.Cell class="text-right">
                                <Button
                                    aria-label="Revoke {grant.application_name} access"
                                    disabled={revokeGrant.isPending}
                                    onclick={() => confirmRevoke(grant)}
                                    size="icon"
                                    title="Revoke access"
                                    variant="ghost"
                                >
                                    <Trash2 class="size-4" aria-hidden="true" />
                                </Button>
                            </Table.Cell>
                        </Table.Row>
                    {/each}
                </Table.Body>
            </Table.Root>
        </div>
    {/if}
</div>

<AlertDialog.Root bind:open={revokeDialogOpen}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Revoke Application Access</AlertDialog.Title>
            <AlertDialog.Description>
                Revoke access for "{selectedGrant?.application_name}"? The application will need to complete OAuth again before it can access your account.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} disabled={revokeGrant.isPending} onclick={() => void revokeSelectedGrant()}>
                Revoke Access
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
