<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';

    import ErrorMessage from '$comp/error-message.svelte';
    import Loading from '$comp/loading.svelte';
    import { H3, H4, Muted } from '$comp/typography';
    import * as Select from '$comp/ui/select';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { organization } from '$features/organizations/context.svelte';
    import {
        getOrganizationProjectsQuery,
        getProjectIntegrationNotificationSettings,
        putProjectIntegrationNotificationSettings
    } from '$features/projects/api.svelte';
    import NotificationSettingsForm from '$features/projects/components/notification-settings-form.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import { patchUser } from '$features/users/api.svelte';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();

    const meQuery = getMeQuery();
    const queryParams = queryParamsState({
        default: { project: '' },
        pushHistory: true,
        schema: { project: 'string' }
    });

    const updateUser = patchUser({
        route: {
            get id() {
                return meQuery.data?.id;
            }
        }
    });

    const projectsQuery = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    const allProjects = $derived(projectsQuery.data?.data ?? []);
    const selectedProject = $derived(allProjects.find((p) => p.id === queryParams.project) ?? allProjects[0]);

    const projectsByOrganization = $derived.by(() => {
        const groups = Object.groupBy(allProjects, (project) => project.organization_name);
        return Object.entries(groups)
            .map(([organizationName, projects]) => ({
                organizationName: organizationName,
                projects: (projects || []).sort((a, b) => a.name.localeCompare(b.name))
            }))
            .sort((a, b) => a.organizationName.localeCompare(b.organizationName));
    });

    const selectedProjectLabel = $derived(() => {
        if (!selectedProject) {
            return 'Select a project...';
        }

        return `${selectedProject.organization_name} / ${selectedProject.name}`;
    });

    const selectedProjectNotificationSettings = $derived(
        selectedProject
            ? getProjectIntegrationNotificationSettings({
                  route: { id: selectedProject.id, integration: 'email' }
              })
            : null
    );

    const updateProjectNotificationSettings = $derived(
        selectedProject
            ? putProjectIntegrationNotificationSettings({
                  route: { id: selectedProject.id, integration: 'email' }
              })
            : null
    );

    const debouncedOnEmailNotificationChanged = debounce(500, onEmailNotificationChanged);
    async function onEmailNotificationChanged(checked: boolean) {
        toast.dismiss(toastId);

        try {
            await updateUser.mutateAsync({ email_notifications_enabled: checked });
            toastId = toast.success('Email notification preference saved.');
        } catch {
            toastId = toast.error('An error occurred while saving your email notification preferences.');
        }
    }

    async function handleProjectNotificationSave(settings: NotificationSettings) {
        if (!updateProjectNotificationSettings) return;
        await updateProjectNotificationSettings.mutateAsync(settings);
    }
</script>

<div class="space-y-6">
    <div>
        <H3>Notifications</H3>
        <Muted>Configure how you receive notifications.</Muted>
    </div>
    <Separator />

    <div class="space-y-2">
        <H3 class="mb-4">Email Notifications</H3>
        <div class="flex flex-row items-center justify-between rounded-lg border p-4">
            <div class="space-y-0.5">
                <H4>Communication emails</H4>
                <Muted>Receive emails about your account activity.</Muted>
            </div>
            {#if meQuery.data}
                <Switch
                    bind:checked={meQuery.data.email_notifications_enabled}
                    id="email_notifications_enabled"
                    disabled={updateUser.isPending}
                    onCheckedChange={debouncedOnEmailNotificationChanged}
                />
            {:else}
                <Skeleton class="h-6 w-12 rounded-full" />
            {/if}
        </div>
        {#if updateUser.isPending}
            <Loading class="ml-2" variant="secondary" />
        {/if}
    </div>

    <Separator />

    <H3 class="mb-4">Project Notification Settings</H3>

    <div class="mb-4">
        <Select.Root bind:value={queryParams.project} type="single">
            <Select.Trigger class="w-full">
                {selectedProjectLabel}
            </Select.Trigger>
            <Select.Content>
                <Select.Item value="">Select a project...</Select.Item>
                {#each projectsByOrganization as { organizationName, projects } (organizationName)}
                    <Select.Group>
                        <Select.Label>{organizationName}</Select.Label>
                        {#each projects as project (project.id)}
                            <Select.Item value={project.id}>{project.name}</Select.Item>
                        {/each}
                    </Select.Group>
                {/each}
            </Select.Content>
        </Select.Root>
    </div>

    <div class="rounded-lg border p-4">
        {#if selectedProject}
            <div class="mb-2 font-semibold">{selectedProject.name}</div>
            <NotificationSettingsForm settings={selectedProjectNotificationSettings?.data} save={handleProjectNotificationSave} />
            {#if selectedProjectNotificationSettings?.error}
                <ErrorMessage message={selectedProjectNotificationSettings.error?.message?.toString() ?? 'Unable to load notification settings.'} />
            {/if}
        {:else if allProjects.length === 0}
            <div class="mb-2 font-semibold">
                {#if projectsQuery.isLoading}
                    Loading projects...
                {:else}
                    No projects found
                {/if}
            </div>
            <NotificationSettingsForm settings={selectedProjectNotificationSettings?.data} save={handleProjectNotificationSave} />
        {:else}
            <div class="mb-2 font-semibold">No Project Selected</div>
            <Muted>Please select a project to edit notification settings.</Muted>
        {/if}
    </div>
</div>
