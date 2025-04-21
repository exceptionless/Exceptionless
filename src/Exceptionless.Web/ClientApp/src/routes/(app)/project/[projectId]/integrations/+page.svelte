<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';

    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import { H3, H4, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { env } from '$env/dynamic/public';
    import { slackOAuthLogin } from '$features/auth/index.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import {
        deleteSlack,
        getProjectIntegrationNotificationSettings,
        getProjectQuery,
        postSlack,
        putProjectIntegrationNotificationSettings
    } from '$features/projects/api.svelte';
    import RemoveSlackDialog from '$features/projects/components/dialogs/remove-slack-dialog.svelte';
    import NotificationSettingsForm from '$features/projects/components/notification-settings-form.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { type GetProjectWebhooksParams, postWebhook } from '$features/webhooks/api.svelte';
    import { getProjectWebhooksQuery } from '$features/webhooks/api.svelte';
    import AddWebhookDialog from '$features/webhooks/components/dialogs/add-webhook-dialog.svelte';
    import { getTableOptions } from '$features/webhooks/components/table/options.svelte';
    import WebhooksDataTable from '$features/webhooks/components/table/webhooks-data-table.svelte';
    import { NewWebhook, Webhook } from '$features/webhooks/models';
    import Slack from '$lib/assets/slack.svg'; // TOOD: Fix the slack icon to be an svg component
    import Plus from '@lucide/svelte/icons/plus';
    import Zapier from '@lucide/svelte/icons/zap';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';

    let toastId = $state<number | string>();
    let showAddWebhookDialog = $state(false);
    let showRemoveSlackDialog = $state(false);
    const projectId = page.params.projectId || '';
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const isSlackEnabled = !!env.PUBLIC_SLACK_APPID;
    const hasSlackIntegration = $derived(projectQuery.data?.has_slack_integration ?? false);
    const newWebhook = postWebhook();

    const addSlackMutation = postSlack({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const removeSlackMutation = deleteSlack({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const slackNotificationSettingsQuery = getProjectIntegrationNotificationSettings({
        route: {
            get id() {
                return projectId;
            },
            integration: 'slack'
        }
    });

    const updateSlackNotificationSettingsResponse = putProjectIntegrationNotificationSettings({
        route: {
            get id() {
                return projectId;
            },
            integration: 'slack'
        }
    });

    async function addWebhook(webhook: NewWebhook) {
        toast.dismiss(toastId);

        try {
            await newWebhook.mutateAsync(webhook);
            toastId = toast.success('Webhook added successfully');
        } catch {
            toastId = toast.error('Error adding webhook. Please try again.');
        }
    }

    async function addSlack() {
        toast.dismiss(toastId);

        try {
            const code = await slackOAuthLogin();
            await addSlackMutation.mutateAsync({ code });
            toastId = toast.success('Successfully connected Slack integration.');
        } catch {
            toastId = toast.error('Error connecting Slack integration. Please try again.');
        }
    }

    async function removeSlack() {
        toast.dismiss(toastId);

        try {
            await removeSlackMutation.mutateAsync();
            toastId = toast.success('Successfully removed Slack integration.');
        } catch {
            toastId = toast.error('Error removing Slack integration. Please try again.');
        }
    }

    async function updateSlackNotificationSettings(settings: NotificationSettings) {
        toast.dismiss(toastId);

        try {
            await updateSlackNotificationSettingsResponse.mutateAsync(settings);
            toastId = toast.success('Successfully updated Slack notification settings.');
        } catch {
            toastId = toast.error('Error updating Slack notification settings. Please try again.');
        }
    }

    const DEFAULT_PARAMS = {
        limit: DEFAULT_LIMIT
    };

    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const webhooksQueryParameters: GetProjectWebhooksParams = $state({
        get limit() {
            return queryParams.limit!;
        },
        set limit(value) {
            queryParams.limit = value;
        }
    });

    const webhooksQuery = getProjectWebhooksQuery({
        get params() {
            return webhooksQueryParameters;
        },
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    const table = createTable(getTableOptions<Webhook>(webhooksQueryParameters, webhooksQuery));

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });
</script>

<div class="space-y-6">
    <div>
        <H3>Integrations</H3>
        <Muted>Create and manage API keys for authenticating your applications with Exceptionless.</Muted>
    </div>
    <Separator />

    <section class="space-y-2">
        <H4>Zapier</H4>
        <Muted
            >Exceptionless has a native Zapier integration. You can use Zapier to connect your Exceptionless account to over 3,000 other applications all
            without writing any code.</Muted
        >

        <Button href="https://zapier.com/apps/exceptionless/integrations" target="_blank"><Zapier class="mr-2 size-4" /> Connect Zapier</Button>
    </section>

    {#if isSlackEnabled}
        <section class="space-y-2">
            <H4>Slack</H4>
            <Muted
                >Integrate Exceptionless with Slack to receive real-time notifications about new errors, critical events, and system alerts directly in your
                team's Slack channels. Keep your team informed and respond faster to issues without constantly checking the dashboard.</Muted
            >

            <NotificationSettingsForm settings={slackNotificationSettingsQuery.data} save={updateSlackNotificationSettings} />

            {#if hasSlackIntegration}
                <Button onclick={() => (showRemoveSlackDialog = true)}><img class="text- mr-2 size-4" alt="Slack" src={Slack} /> Remove Slack</Button>
            {:else}
                <Button onclick={addSlack}><img class="text- mr-2 size-4" alt="Slack" src={Slack} /> Connect Slack</Button>
            {/if}
        </section>
    {/if}

    <section class="space-y-2">
        <div class="flex items-start justify-between">
            <div>
                <H4>Webhooks</H4>
                <Muted>The following web hooks will be called for this project.</Muted>
            </div>

            <Button variant="secondary" size="icon" onclick={() => (showAddWebhookDialog = true)} title="Add Webhook">
                <Plus class="size-4" />
            </Button>
        </div>

        <WebhooksDataTable bind:limit={webhooksQueryParameters.limit!} isLoading={webhooksQuery.isLoading} {table} />
    </section>
</div>

{#if showAddWebhookDialog && organization.current}
    <AddWebhookDialog bind:open={showAddWebhookDialog} save={addWebhook} {projectId} organizationId={organization.current} />
{/if}

{#if showRemoveSlackDialog}
    <RemoveSlackDialog bind:open={showRemoveSlackDialog} remove={removeSlack} />
{/if}
