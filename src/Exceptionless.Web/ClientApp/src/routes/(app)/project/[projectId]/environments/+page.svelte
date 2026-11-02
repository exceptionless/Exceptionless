<script lang="ts">
    import { page } from '$app/state';
    import { toast } from 'svelte-sonner';

    import type { DiscardedEnvironment, UpdateProjectEnvironmentSettings } from '#features/projects/models.ts';

    import { A, H4, Muted } from '#comp/typography/index.ts';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { buttonVariants } from '#comp/ui/button/index.ts';
    import { getProjectCountQuery } from '#features/events/api.svelte.ts';
    import {
        deleteProjectEnvironment,
        getProjectDiscardedEnvironments,
        getProjectQuery,
        patchProjectEnvironment,
        postProjectEnvironment,
        putProjectEnvironmentSettings
    } from '#features/projects/api.svelte.ts';
    import AddEnvironmentForm from '#features/projects/components/environments/add-environment-form.svelte';
    import DiscardedEnvironmentsTable from '#features/projects/components/environments/discarded-environments-table.svelte';
    import EnvironmentSettings from '#features/projects/components/environments/environment-settings.svelte';
    import EnvironmentsTable from '#features/projects/components/environments/environments-table.svelte';
    import { buildEnvironmentRows, type EnvironmentRow, splitDiscardedEnvironments } from '#features/projects/environments.ts';
    import * as agg from '#features/shared/api/aggregations.ts';

    const projectId = $derived(page.params.projectId || '');
    const request = {
        route: {
            get id() {
                return projectId;
            }
        }
    };

    const projectQuery = getProjectQuery(request);
    const discardedQuery = getProjectDiscardedEnvironments(request);
    const observedQuery = getProjectCountQuery({
        params: {
            aggregations: 'terms:(environment~100 max:date)',
            time: '[now-30d TO now]'
        },
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    const addEnvironment = postProjectEnvironment(request);
    const updateEnvironment = patchProjectEnvironment(request);
    const removeEnvironment = deleteProjectEnvironment(request);
    const updateSettings = putProjectEnvironmentSettings(request);

    let environmentToRemove = $state<EnvironmentRow>();
    let showRemoveDialog = $state(false);

    const project = $derived(projectQuery.data);
    const busy = $derived(addEnvironment.isPending || updateEnvironment.isPending || removeEnvironment.isPending || updateSettings.isPending);
    const observed = $derived(
        agg.terms(observedQuery.data?.aggregations, 'terms_environment')?.buckets?.map((bucket) => ({
            count: bucket.total ?? 0,
            key: bucket.key,
            lastSeen: agg.max<string>(bucket.aggregations, 'max_date')?.value
        })) ?? []
    );
    const rows = $derived(buildEnvironmentRows(project?.environments, observed));
    const discarded = $derived(splitDiscardedEnvironments(discardedQuery.data?.environments));

    async function add(name: string, display = name) {
        try {
            await addEnvironment.mutateAsync({
                name,
                notify: project?.notify_other_environments ?? true
            });
            toast.success(`Added ${display}.`);
        } catch {
            toast.error(`Unable to add ${display}. Please try again.`);
        }
    }

    async function allow(environment: DiscardedEnvironment) {
        await add(environment.name ?? environment.key!, environment.name ?? environment.key!);
    }

    async function change(settings: UpdateProjectEnvironmentSettings) {
        try {
            await updateSettings.mutateAsync(settings);
            toast.success('Environment settings saved.');
        } catch {
            toast.error('Unable to save the environment settings. Please try again.');
        }
    }

    function confirmRemove(row: EnvironmentRow) {
        environmentToRemove = row;
        showRemoveDialog = true;
    }

    async function remove() {
        if (!environmentToRemove) {
            return;
        }

        const name = environmentToRemove.name;
        try {
            await removeEnvironment.mutateAsync(name);
            toast.success(`Removed ${name}. Its events were not changed.`);
            showRemoveDialog = false;
            environmentToRemove = undefined;
        } catch {
            toast.error(`Unable to remove ${name}. Please try again.`);
        }
    }

    async function toggleNotify(row: EnvironmentRow, notify: boolean) {
        try {
            await updateEnvironment.mutateAsync({
                changes: {
                    notify
                },
                name: row.name
            });
        } catch {
            toast.error(`Unable to update notifications for ${row.name}. Please try again.`);
        }
    }
</script>

<div class="space-y-6">
    <section class="space-y-2">
        <H4>Environments</H4>
        <Muted>
            Clients report the <A href="https://exceptionless.com/docs/environments/" target="_blank">environment</A> an event came from, such as production or staging.
            List an environment to show it with the casing you choose, choose whether it sends notifications, or discard events from environments you have not listed.
            Names are not case-sensitive, and adding or removing a name never changes or deletes events.
        </Muted>
    </section>

    <section class="space-y-3">
        <H4>Add Environment</H4>
        <AddEnvironmentForm add={(environment) => addEnvironment.mutateAsync(environment)} listedNames={project?.environments?.map((e) => e.name) ?? []} />
    </section>

    <section class="space-y-3">
        <H4>Settings</H4>
        {#if project}
            <EnvironmentSettings
                disabled={busy}
                discardEventsWithoutEnvironment={project.discard_events_without_environment}
                discardUnlistedEnvironments={project.discard_unlisted_environments}
                notifyOtherEnvironments={project.notify_other_environments}
                update={change}
            />
        {/if}
    </section>

    <section class="space-y-3">
        <H4>Environments in Use</H4>
        <Muted>Listed environments and the environments seen in this project's events over the last 30 days.</Muted>
        <EnvironmentsTable
            add={(row) => add(row.name)}
            {busy}
            isError={projectQuery.isError || observedQuery.isError}
            isLoading={projectQuery.isLoading || observedQuery.isLoading}
            notifyOtherEnvironments={project?.notify_other_environments ?? true}
            remove={confirmRemove}
            {rows}
            {toggleNotify}
        />
    </section>

    <section class="space-y-3">
        <H4>Discarded Events</H4>
        <Muted>
            Clients are told an event was accepted before it is checked, so discarded events are only visible here. Allow an environment to start accepting its
            events, for example when a name was misspelled in a client.
        </Muted>
        <DiscardedEnvironmentsTable
            allow={(environment) => allow(environment)}
            {busy}
            isError={discardedQuery.isError}
            isLoading={discardedQuery.isLoading}
            named={discarded.named}
            retentionDays={discardedQuery.data?.retention_days ?? 7}
            stopDiscardingUnspecified={() =>
                change({
                    discard_events_without_environment: false
                })}
            unspecified={discarded.unspecified}
        />
    </section>
</div>

<AlertDialog.Root bind:open={showRemoveDialog}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Remove Environment</AlertDialog.Title>
            <AlertDialog.Description>
                Remove <span class="font-medium">{environmentToRemove?.name}</span> from the list? Its events are not changed or deleted. New events from it will
                follow the settings for environments that are not listed.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
                class={buttonVariants({
                    variant: 'destructive'
                })}
                onclick={remove}>Remove Environment</AlertDialog.Action
            >
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
