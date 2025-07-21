<script lang="ts">
    // --- Type imports (always first) ---
    import type { NotificationSettings, ViewProject } from '$features/projects/models';

    // --- UI components ---
    import ErrorMessage from '$comp/error-message.svelte';
    import Loading from '$comp/loading.svelte';
    import { H3, H4, Muted } from '$comp/typography';
    import * as Select from '$comp/ui/select';
    import { Separator } from '$comp/ui/separator';
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
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';
    import { SvelteMap } from 'svelte/reactivity';

    // --- User and project state ---
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

    // Simple state for projects and selections
    let emailNotificationsEnabled = $state(false);
    let selectedProjectId = $state(queryParams.project ?? '');

    // Load projects for current organization
    const projectsQuery = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    // Derived state for projects and selected project
    const allProjects = $derived(projectsQuery.data?.data ?? []);
    const selectedProject = $derived(allProjects.find((p) => p.id === selectedProjectId));

    // Auto-select first project if none selected and projects are available
    $effect(() => {
        if (allProjects.length > 0 && !selectedProjectId) {
            selectedProjectId = allProjects[0]!.id;
        }
    });

    // Group projects by organization
    const projectsByOrganization = $derived.by(() => {
        const groups = new SvelteMap<string, ViewProject[]>();
        for (const project of allProjects) {
            const orgName = project.organization_name;
            if (!groups.has(orgName)) {
                groups.set(orgName, []);
            }
            groups.get(orgName)!.push(project);
        }
        return Array.from(groups.entries())
            .map(([orgName, projects]) => ({
                organizationName: orgName,
                projects: projects.sort((a, b) => a.name.localeCompare(b.name))
            }))
            .sort((a, b) => a.organizationName.localeCompare(b.organizationName));
    });

    const selectedProjectLabel = $derived(() => {
        if (!selectedProject) return 'Select a project...';
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

    // Sync user email notifications to local state
    $effect(() => {
        if (meQuery.data?.email_notifications_enabled !== undefined) {
            emailNotificationsEnabled = meQuery.data.email_notifications_enabled;
        }
    });

    // Sync selected project with query params
    $effect(() => {
        if (selectedProjectId !== queryParams.project) {
            queryParams.project = selectedProjectId;
        }
    });

    // Initialize selected project from query params when projects load
    $effect(() => {
        if (allProjects.length > 0 && queryParams.project && queryParams.project !== selectedProjectId) {
            const projectExists = allProjects.some((p) => p.id === queryParams.project);
            if (projectExists) {
                selectedProjectId = queryParams.project;
            }
        }
    });

    // --- Handlers ---
    async function handleEmailNotificationChange(checked: boolean) {
        if (!meQuery.data?.id) return;

        emailNotificationsEnabled = checked;

        try {
            await updateUser.mutateAsync({ email_notifications_enabled: checked });
            toast.success('Email notification preference saved.');
        } catch (error: unknown) {
            // Revert the change on error
            emailNotificationsEnabled = !checked;

            const errorMessage =
                error instanceof ProblemDetails ? String(error.detail ?? error.message ?? 'Error saving preference.') : 'Error saving preference.';
            toast.error(errorMessage);
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
            <Switch
                bind:checked={emailNotificationsEnabled}
                id="email_notifications_enabled"
                disabled={updateUser.isPending}
                onCheckedChange={handleEmailNotificationChange}
            />
        </div>
        {#if updateUser.isPending}
            <Loading class="ml-2" variant="secondary" />
        {/if}
    </div>

    <Separator />

    <H3 class="mb-4">Project Notification Settings</H3>

    <div class="mb-4">
        <Select.Root bind:value={selectedProjectId} type="single">
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
            {#if selectedProjectNotificationSettings?.data}
                <NotificationSettingsForm settings={selectedProjectNotificationSettings.data} save={handleProjectNotificationSave} />
            {:else if selectedProjectNotificationSettings?.error}
                <ErrorMessage message={selectedProjectNotificationSettings.error?.message?.toString() ?? 'Unable to load notification settings.'} />
            {:else}
                <!-- Show loading skeleton -->
                <NotificationSettingsForm save={handleProjectNotificationSave} />
            {/if}
        {:else if allProjects.length === 0}
            <div class="mb-2 font-semibold">
                {#if projectsQuery.isLoading}
                    Loading projects...
                {:else}
                    No projects found
                {/if}
            </div>
            <!-- Show loading skeleton while projects are loading -->
            <NotificationSettingsForm save={handleProjectNotificationSave} />
        {:else}
            <div class="mb-2 font-semibold">No Project Selected</div>
            <Muted>Please select a project to edit notification settings.</Muted>
        {/if}
    </div>
</div>
