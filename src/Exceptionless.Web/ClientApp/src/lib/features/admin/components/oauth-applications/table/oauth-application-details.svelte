<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';

    import { resolve } from '$app/paths';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';

    let { application }: { application: OAuthApplication } = $props();
</script>

<div id={`oauth-application-details-${application.id}`} class="flex flex-col gap-4 p-3">
    <dl class="grid gap-4 text-sm md:grid-cols-2">
        <div class="flex min-w-0 flex-col gap-1">
            <dt class="text-muted-foreground">Client ID</dt>
            <dd class="break-all"><code class="text-xs">{application.client_id}</code></dd>
        </div>
        <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground">Status</dt>
            <dd>{application.is_disabled ? 'Disabled' : 'Enabled'}</dd>
        </div>
        <div class="flex min-w-0 flex-col gap-1">
            <dt class="text-muted-foreground">Redirect URLs</dt>
            <dd>
                <ul class="flex flex-col gap-1">
                    {#each application.redirect_uris as uri (uri)}
                        <li class="break-all"><code class="text-xs">{uri}</code></li>
                    {/each}
                </ul>
            </dd>
        </div>
        <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground">Allowed scopes</dt>
            <dd class="flex flex-wrap gap-1">
                {#each application.scopes as scope (scope)}
                    <Badge variant="secondary">{scope}</Badge>
                {/each}
            </dd>
        </div>
        <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground">Created</dt>
            <dd><DateTime value={application.created_utc} /></dd>
        </div>
        <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground">Updated</dt>
            <dd><DateTime value={application.updated_utc} /></dd>
        </div>
        {#if application.notes}
            <div class="flex flex-col gap-1 md:col-span-2">
                <dt class="text-muted-foreground">Notes</dt>
                <dd class="break-words whitespace-pre-wrap">{application.notes}</dd>
            </div>
        {/if}
    </dl>
    <div>
        <Button
            variant="outline"
            size="sm"
            href={resolve('/(app)/system/oauth-applications/[id=objectid]', {
                id: application.id
            })}>Edit application</Button
        >
    </div>
</div>
