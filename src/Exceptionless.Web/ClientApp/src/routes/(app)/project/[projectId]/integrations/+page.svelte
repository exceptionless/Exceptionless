<script lang="ts">
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import { H3, H4, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { env } from '$env/dynamic/public';
    import { organization } from '$features/organizations/context.svelte';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { postWebhook } from '$features/webhooks/api.svelte';
    import { getProjectWebhooksQuery } from '$features/webhooks/api.svelte';
    import AddWebhookDialog from '$features/webhooks/components/dialogs/add-webhook-dialog.svelte';
    import { getTableContext } from '$features/webhooks/components/table/options.svelte';
    import WebhooksDataTable from '$features/webhooks/components/table/webhooks-data-table.svelte';
    import { NewWebhook, Webhook } from '$features/webhooks/models';
    import Slack from '$lib/assets/slack.svg'; // TOOD: Fix the slack icon to be an svg component
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import Plus from 'lucide-svelte/icons/plus';
    import Zapier from 'lucide-svelte/icons/zap';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    let showAddWebhookDialog = $state(false);
    const projectId = page.params.projectId || '';
    const projectResponse = getProjectQuery({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const isSlackEnabled = !!env.PUBLIC_SLACK_APPID;
    const hasSlackIntegration = $derived(projectResponse.data?.has_slack_integration ?? false);
    const newWebhook = postWebhook();

    async function addWebhook(webhook: NewWebhook) {
        try {
            await newWebhook.mutateAsync(webhook);
            toast.success('Webhook added successfully');
        } catch {
            toast.error('Error adding webhook. Please try again.');
        }
    }

    async function addSlack() {
        /*

 .confirmDanger(
                            translateService.T("Are you sure you want to remove slack support?"),
                            translateService.T("Remove Slack")
                        )

         function onSuccess(response) {
                    return Restangular.one("projects", id).post("slack", null, { code: response.code });
                }

                return $auth.link("slack").then(onSuccess);
                */
    }

    async function removeSlack() {
        //return Restangular.one("projects", id).one("slack").remove();
    }

    const DEFAULT_PARAMS = {
        limit: DEFAULT_LIMIT
    };

    const params = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const context = getTableContext<Webhook>({ limit: params.limit! });
    const table = createTable(context.options);

    const webhooksQuery = getProjectWebhooksQuery({
        params: context.parameters,
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    watch(
        () => webhooksQuery.dataUpdatedAt,
        () => {
            if (webhooksQuery.isSuccess) {
                context.data = webhooksQuery.data.data || [];
                context.meta = webhooksQuery.data.meta;
            }
        }
    );
</script>

<div class="space-y-6">
    <div>
        <H3>Integrations</H3>
        <Muted>Create and manage API keys for authenticating your applications with Exceptionless.</Muted>
    </div>
    <Separator />

    <section class="space-y-2">
        <H4>Zapier</H4>
        <P
            >Exceptionless has a native Zapier integration. You can use Zapier to connect your Exceptionless account to over 3,000 other applications all
            without writing any code.</P
        >

        <Button href="https://zapier.com/apps/exceptionless/integrations" target="_blank"><Zapier class="mr-2 size-4" /> Connect Zapier</Button>
    </section>

    {#if isSlackEnabled}
        <section class="space-y-2">
            <H4>Slack</H4>
            <P
                >Integrate Exceptionless with Slack to receive real-time notifications about new errors, critical events, and system alerts directly in your
                team's Slack channels. Keep your team informed and respond faster to issues without constantly checking the dashboard.</P
            >

            {#if hasSlackIntegration}
                <Button onclick={removeSlack}><img class="text- mr-2 size-4" alt="Slack" src={Slack} /> Connect Slack</Button>
            {:else}
                <Button onclick={addSlack}><img class="text- mr-2 size-4" alt="Slack" src={Slack} /> Connect Slack</Button>
            {/if}
        </section>
    {/if}

    <section class="space-y-2">
        <H4>Webhooks</H4>
        <P>The following web hooks will be called for this project.</P>

        <WebhooksDataTable bind:limit={params.limit!} isLoading={webhooksQuery.isLoading} {table}>
            {#snippet footerChildren()}
                <div class="h-9 min-w-[140px]">
                    <Button size="sm" onclick={() => (showAddWebhookDialog = true)}>
                        <Plus class="mr-2 size-4" />
                        Add Webhook</Button
                    >
                </div>

                <DataTable.PageSize bind:value={params.limit!} {table}></DataTable.PageSize>
                <div class="flex items-center space-x-6 lg:space-x-8">
                    <DataTable.PageCount {table} />
                    <DataTable.Pagination {table} />
                </div>
            {/snippet}
        </WebhooksDataTable>
    </section>
</div>

{#if showAddWebhookDialog && organization.current}
    <AddWebhookDialog bind:open={showAddWebhookDialog} save={addWebhook} {projectId} organizationId={organization.current} />
{/if}
