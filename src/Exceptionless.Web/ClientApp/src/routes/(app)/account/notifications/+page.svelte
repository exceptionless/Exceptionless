<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import Loading from '$comp/loading.svelte';
    import Switch from '$comp/primitives/switch.svelte';
    import { H3, H4, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { User } from '$features/users/models';
    import { useFetchClientStatus } from '$shared/api/api.svelte';
    import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';

    const data = $state(new User());
    data.email_notifications_enabled = true;

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);

    let problem = $state(new ProblemDetails());

    async function onSave() {
        if (client.isLoading) {
            return;
        }
    }
</script>

<div class="space-y-6">
    <div>
        <H3>Notifications</H3>
        <Muted>Configure how you receive notifications.</Muted>
    </div>
    <Separator />

    <form class="space-y-2" onsubmit={onSave}>
        <ErrorMessage message={problem.errors.general}></ErrorMessage>

        <H4 class="mb-4">Email Notifications</H4>
        <div class="flex flex-row items-center justify-between rounded-lg border p-4">
            <div class="space-y-0.5">
                <H4>Communication emails</H4>
                <Muted>Receive emails about your account activity.</Muted>
            </div>
            <Switch bind:checked={data.email_notifications_enabled} id="email_notifications_enabled"></Switch>
        </div>

        <div class="pt-2">
            <Button type="submit">
                {#if clientStatus.isLoading}
                    <Loading class="mr-2" variant="secondary"></Loading> Saving...
                {:else}
                    Save
                {/if}
            </Button>
        </div>
    </form>
</div>
