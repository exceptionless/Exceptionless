<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';

    import { A, H3, Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import * as Select from '$comp/ui/select';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { getProjectsQuery, getProjectUserNotificationSettings, postProjectUserNotificationSettings } from '$features/projects/api.svelte';
    import UserNotificationSettingsForm from '$features/projects/components/user-notification-settings-form.svelte';
    import AlertDescription from '$features/shared/components/ui/alert/alert-description.svelte';
    import AlertTitle from '$features/shared/components/ui/alert/alert-title.svelte';
    import Alert from '$features/shared/components/ui/alert/alert.svelte';
    import { getMeQuery, patchUser, resendVerificationEmail } from '$features/users/api.svelte';
    import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
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

    async function onProjectNotificationChanged(settings: NotificationSettings) {
        toast.dismiss(toastId);

        try {
            await updateProjectNotificationSettings.mutateAsync(settings);
            toastId = toast.success('Project notification preference saved.');
        } catch {
            toastId = toast.error('An error occurred while saving your project notification preferences.');
        }
    }

    const isEmailAddressVerified = $derived(meQuery.data?.is_email_address_verified ?? false);
    let emailNotificationsEnabled = $derived(meQuery.data?.email_notifications_enabled ?? false);
    $effect(() => {
        emailNotificationsEnabled = meQuery.data?.email_notifications_enabled ?? false;
    });

    const resendVerificationEmailMutation = resendVerificationEmail({
        route: {
            get id() {
                return meQuery.data?.id;
            }
        }
    });

    async function handleResendVerificationEmail() {
        toast.dismiss(toastId);
        try {
            await resendVerificationEmailMutation.mutateAsync();
            toastId = toast.success('Please check your inbox for the verification email.');
        } catch {
            toastId = toast.error('Error sending verification email. Please try again.');
        }
    }

    async function handleUpgrade() {
        console.log('TODO: Upgrade to premium features');
    }
</script>

<div class="space-y-6">
    <div>
        <H3>Notifications</H3>
        <Muted>Configure how you receive notifications.</Muted>
    </div>
    <Separator />

    {#if meQuery.isSuccess && (!isEmailAddressVerified || !emailNotificationsEnabled)}
        <Alert variant="destructive" class="mb-4">
            <AlertCircleIcon />
            <AlertTitle>Email notifications are currently disabled</AlertTitle>
            {#if !isEmailAddressVerified}
                <AlertDescription>
                    <span
                        >To enable email notifications you must first verify your email address. <A class="inline" onclick={handleResendVerificationEmail}
                            >Resend verification email.</A
                        ></span
                    >
                </AlertDescription>
            {/if}
        </Alert>
    {/if}

    <div class="space-y-2">
        <H3>Email Notifications</H3>
        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="flex items-center gap-2">
                        <div class="text-sm font-medium">Enable Email Notifications</div>
                        {#if !isEmailAddressVerified}
                            <Badge variant="secondary" class="text-xs">Requires verification</Badge>
                        {/if}
                    </div>
                    <Muted class="text-xs">Receive updates about activity in your organization and projects.</Muted>
                </div>
                {#if meQuery.data}
                    <Switch
                        bind:checked={emailNotificationsEnabled}
                        id="email_notifications_enabled"
                        disabled={updateUser.isPending}
                        onCheckedChange={debouncedOnEmailNotificationChanged}
                    />
                {:else}
                    <Skeleton class="h-[1.15rem] w-8 rounded-full" />
                {/if}
            </div>
        </div>
    </div>

    {#if selectedProject}
        <div class="space-y-2">
            <H3>Project Notifications</H3>
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

        <UserNotificationSettingsForm
            upgrade={handleUpgrade}
            settings={selectedProjectNotificationSettings?.data}
            save={onProjectNotificationChanged}
            {emailNotificationsEnabled}
            hasPremiumFeatures={selectedProject.has_premium_features}
        />
    {/if}
</div>
