<script lang="ts">
    import { page } from '$app/state';
    import * as Card from '$comp/ui/card';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { watch } from 'runed';

    watch(
        () => organization.current,
        () => {
            // TODO: Redirect?
        },
        { lazy: true }
    );

    function onFilterChanged() {}

    function close() {
        /*

        if (response && response.status === 426) {
                            return billingService.confirmUpgradePlan(response.data.message).then(
                                function () {
                                    return getEvent();
                                },
                                function () {
                                    $state.go("app.frequent");
                                }
                            );
                        }

                        $state.go("app.frequent");
                        notificationService.error(
                            translateService.T("Cannot_Find_Event", { eventId: $stateParams.id })
                        );
         */
    }
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title class="p-6 pb-0 text-2xl" level={2}>Event Details</Card.Title>
        <Card.Content class="pt-4">
            <EventsOverview changed={onFilterChanged} id={page.params.id || ''} {close}></EventsOverview>
        </Card.Content>
    </Card.Root>
</div>
