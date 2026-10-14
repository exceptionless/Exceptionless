<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import EyeIcon from '@lucide/svelte/icons/eye';

    import type { NotificationProps } from '#comp/notification/index.ts';
    import type { ViewOrganization } from '#features/organizations/models.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import { organization } from '#features/organizations/context.svelte.ts';

    interface Props extends NotificationProps {
        name: string;
        userOrganizations: ViewOrganization[];
    }

    let { name, userOrganizations, ...restProps }: Props = $props();

    async function stopImpersonating(): Promise<void> {
        organization.current = userOrganizations[0]?.id;
        await goto(resolve('/(app)'));
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
