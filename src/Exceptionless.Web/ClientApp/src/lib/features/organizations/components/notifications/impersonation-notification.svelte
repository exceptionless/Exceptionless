<script lang="ts">
    import type { NotificationProps } from '$comp/notification';
    import type { ViewOrganization } from '$features/organizations/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { Button } from '$comp/ui/button';
    import { organization } from '$features/organizations/context.svelte';
    import EyeIcon from '@lucide/svelte/icons/eye';

    interface Props extends NotificationProps {
        name: string;
        userOrganizations: ViewOrganization[];
    }

    let { name, userOrganizations, ...restProps }: Props = $props();

    async function stopImpersonating() {
        if (userOrganizations.length > 0) {
            const defaultOrganization = userOrganizations[0]!;
            organization.current = defaultOrganization.id;
            await goto(resolve('/(app)/organization/[organizationId]/manage', { organizationId: defaultOrganization.id }));
        }
    }
</script>

<Notification variant="impersonation" {...restProps}>
    {#snippet icon()}<EyeIcon />{/snippet}
    {#snippet action()}
        {#if userOrganizations.length > 0}
            <Button variant="outline" size="sm" onclick={stopImpersonating}>Stop Impersonating</Button>
        {/if}
    {/snippet}
    <NotificationTitle>Impersonating {name}</NotificationTitle>
    <NotificationDescription>You are viewing this organization as a global admin.</NotificationDescription>
</Notification>
