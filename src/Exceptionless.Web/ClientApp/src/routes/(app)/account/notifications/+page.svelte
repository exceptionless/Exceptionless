<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';

    import { H3, H4, Muted } from '$comp/typography';
    import * as Select from '$comp/ui/select';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { getProjectsQuery, getProjectUserNotificationSettings, postProjectUserNotificationSettings } from '$features/projects/api.svelte';
    import UserNotificationSettingsForm from '$features/projects/components/user-notification-settings-form.svelte';
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

    const projectsQuery = getProjectsQuery({});
    const allProjects = $derived(projectsQuery.data?.data ?? []);

    const projectsByOrganization = $derived.by(() => {
        const groups = Object.groupBy(allProjects, (project) => project.organization_name);
        return Object.entries(groups)
            .map(([organizationName, projects]) => ({
                organizationName: organizationName,
                projects: (projects || []).sort((a, b) => a.name.localeCompare(b.name))
            }))
            .sort((a, b) => a.organizationName.localeCompare(b.organizationName));
    });

    const selectedProject = $derived(allProjects.find((p) => p.id === queryParams.project) ?? allProjects[0]);
    const selectedProjectNotificationSettings = getProjectUserNotificationSettings({
        route: {
            get id() {
                return selectedProject?.id;
            },
            get userId() {
                return meQuery.data?.id;
            }
        }
    });

    const updateProjectNotificationSettings = postProjectUserNotificationSettings({
        route: {
            get id() {
                return selectedProject?.id;
            },
            get userId() {
                return meQuery.data?.id;
            }
        }
    });

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
    </div>

    {#if selectedProject}
        <Separator />

        <div class="space-y-2">
        <H3>Project Notification Settings</H3>
        <Muted>Choose how often you want to receive notifications for event occurrences in this project.</Muted>
        <Select.Root bind:value={queryParams.project!} type="single">
            <Select.Trigger class="w-full">
                {selectedProject.name}
            </Select.Trigger>
            <Select.Content>
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

        <UserNotificationSettingsForm settings={selectedProjectNotificationSettings?.data} save={handleProjectNotificationSave} />
    {/if}
</div>
