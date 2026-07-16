<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';
    import type { ViewRateNotificationRule } from '$features/rate-notifications/types';

    import { A, H3, Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import * as Dialog from '$comp/ui/dialog';
    import * as Select from '$comp/ui/select';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { showUpgradeDialog } from '$features/billing/upgrade-required.svelte';
    import { getProjectsQuery, getProjectUserNotificationSettings, postProjectUserNotificationSettings } from '$features/projects/api.svelte';
    import UserNotificationSettingsForm from '$features/projects/components/user-notification-settings-form.svelte';
    import RateNotificationRuleForm from '$features/rate-notifications/components/rate-notification-rule-form.svelte';
    import RateNotificationRuleList from '$features/rate-notifications/components/rate-notification-rule-list.svelte';
    import AlertDescription from '$features/shared/components/ui/alert/alert-description.svelte';
    import AlertTitle from '$features/shared/components/ui/alert/alert-title.svelte';
    import Alert from '$features/shared/components/ui/alert/alert.svelte';
    import { getMeQuery, patchUser, resendVerificationEmail } from '$features/users/api.svelte';
    import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();

    // Rate notification dialog state
    let rateRuleDialogOpen = $state(false);
    let editingRateRule = $state<undefined | ViewRateNotificationRule>(undefined);

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
    const rateNotificationsEnabled = $derived(selectedProject?.has_rate_notifications ?? false);
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

    function handleUpgrade() {
        if (selectedProject?.organization_id) {
            showUpgradeDialog(selectedProject.organization_id, 'Please upgrade your plan to enable occurrence level notifications.');
        }
    }

    function handleRateRuleUpgrade() {
        if (selectedProject?.organization_id) {
            showUpgradeDialog(selectedProject.organization_id, 'Please upgrade your plan to enable personal rate notification rules.');
        }
    }

    function openCreateRateRule() {
        editingRateRule = undefined;
        rateRuleDialogOpen = true;
    }

    function openEditRateRule(rule: ViewRateNotificationRule) {
        editingRateRule = rule;
        rateRuleDialogOpen = true;
    }

    function closeRateRuleDialog() {
        rateRuleDialogOpen = false;
        editingRateRule = undefined;
    }

    $effect(() => {
        if (rateRuleDialogOpen && editingRateRule && editingRateRule.project_id !== selectedProject?.id) {
            closeRateRuleDialog();
        }
    });
</script>

<div class="space-y-6">
    <div>
        <Muted>Configure how you receive notifications</Muted>
    </div>

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

        {#if rateNotificationsEnabled}
            <div class="space-y-2">
                <H3>Rate Notifications</H3>
                <Muted>Get notified when event rates for this project exceed your custom thresholds.</Muted>
            </div>

            <RateNotificationRuleList
                hasPremiumFeatures={selectedProject.has_premium_features}
                projectId={selectedProject.id}
                userId={meQuery.data?.id}
                upgrade={handleRateRuleUpgrade}
                onCreateClick={openCreateRateRule}
                onEditClick={openEditRateRule}
            />
        {/if}
    {/if}
</div>

{#if rateNotificationsEnabled}
    <Dialog.Root bind:open={rateRuleDialogOpen} onOpenChange={(open) => !open && closeRateRuleDialog()}>
        <Dialog.Content class="max-h-[90vh] overflow-y-auto sm:max-w-lg">
            <Dialog.Header>
                <Dialog.Title>{editingRateRule ? 'Edit Rate Notification Rule' : 'Create Rate Notification Rule'}</Dialog.Title>
                <Dialog.Description>
                    {editingRateRule ? 'Update the rule settings below.' : 'Configure when you want to receive an email notification based on event rates.'}
                </Dialog.Description>
            </Dialog.Header>
            {#if selectedProject}
                <RateNotificationRuleForm
                    hasPremiumFeatures={selectedProject.has_premium_features}
                    projectId={selectedProject.id}
                    rule={editingRateRule}
                    userId={meQuery.data?.id}
                    upgrade={handleRateRuleUpgrade}
                    onSaved={closeRateRuleDialog}
                    onCancel={closeRateRuleDialog}
                />
            {/if}
        </Dialog.Content>
    </Dialog.Root>
{/if}
