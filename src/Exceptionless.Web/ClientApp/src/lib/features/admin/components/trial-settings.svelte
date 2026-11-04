<script lang="ts">
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { toast } from 'svelte-sonner';

    import { Badge } from '#comp/ui/badge/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { Textarea } from '#comp/ui/textarea/index.ts';
    import { getTrialSettingsQuery, putTrialSettingsMutation } from '#features/admin/api.svelte.ts';

    const settingsQuery = getTrialSettingsQuery();
    const updateSettings = putTrialSettingsMutation();
    let blockedEmailDomains = $state('');
    let error = $state<null | string>(null);
    let loadedSettingsKey = $state<null | string>(null);

    const settings = $derived(settingsQuery.data);
    const settingsKey = $derived(settings ? JSON.stringify([settings.blocked_email_domains, settings.is_default]) : null);
    const enteredDomains = $derived(parseDomains(blockedEmailDomains));
    const hasChanges = $derived(!!settings && enteredDomains.join('\n') !== settings.blocked_email_domains.join('\n'));

    $effect(() => {
        if (!settings || loadedSettingsKey === settingsKey) {
            return;
        }

        loadedSettingsKey = settingsKey;
        blockedEmailDomains = settings.blocked_email_domains.join('\n');
    });

    function parseDomains(value: string): string[] {
        return [
            ...new Set(
                value
                    .split(/[\s,]+/)
                    .map((domain) => domain.trim().replace(/^@+/, '').toLowerCase())
                    .filter((domain) => domain.length > 0)
            )
        ].sort();
    }

    async function save(domains: null | string[]) {
        error = null;
        try {
            const saved = await updateSettings.mutateAsync({
                blocked_email_domains: domains
            });
            blockedEmailDomains = saved.blocked_email_domains.join('\n');
            toast.success(saved.is_default ? 'Blocked trial domains reset to the built-in list.' : 'Blocked trial domains saved.');
        } catch (caught: unknown) {
            const message =
                caught instanceof ProblemDetails
                    ? Object.values(caught.errors ?? {})
                          .flat()
                          .join(' ') || caught.title
                    : undefined;
            error = message || 'Failed to update blocked trial domains.';
        }
    }
</script>

{#if settingsQuery.isPending}
    <div class="flex items-center gap-2 p-4">
        <Spinner />
        <span class="text-muted-foreground text-sm">Loading free trial settings...</span>
    </div>
{:else if settingsQuery.isError}
    <div class="p-4">
        <p class="text-destructive text-sm">Failed to load free trial settings.</p>
    </div>
{:else if settings}
    <Field.Field orientation="vertical" class="gap-4 p-4">
        <Field.Content>
            <div class="flex flex-wrap items-center gap-2">
                <Field.Label for="trial-blocked-email-domains">Blocked trial email domains</Field.Label>
                <Badge variant={settings.is_default ? 'outline' : 'secondary'}>{settings.is_default ? 'Built-in list' : 'Custom list'}</Badge>
            </div>
            <Field.Description>
                Signups from these domains and their subdomains get the Free plan instead of a free trial. Enter one domain per line.
            </Field.Description>
        </Field.Content>
        <Textarea
            id="trial-blocked-email-domains"
            bind:value={blockedEmailDomains}
            rows={8}
            class="font-mono text-sm"
            aria-invalid={error ? 'true' : undefined}
            aria-describedby={error ? 'trial-blocked-email-domains-error' : undefined}
            disabled={updateSettings.isPending}
        />
        {#if error}
            <p id="trial-blocked-email-domains-error" class="text-destructive text-sm">{error}</p>
        {/if}
        <div class="flex flex-wrap items-center justify-between gap-2">
            <span class="text-muted-foreground text-sm">{enteredDomains.length === 1 ? '1 domain' : `${enteredDomains.length} domains`}</span>
            <div class="flex flex-wrap items-center gap-2">
                {#if !settings.is_default}
                    <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        aria-label="Reset blocked trial domains to the built-in list"
                        disabled={updateSettings.isPending}
                        onclick={() => save(null)}>Reset</Button
                    >
                {/if}
                <Button
                    type="button"
                    size="sm"
                    aria-label="Save blocked trial domains"
                    disabled={updateSettings.isPending || !hasChanges}
                    onclick={() => save(enteredDomains)}
                >
                    {updateSettings.isPending ? 'Saving...' : 'Save'}
                </Button>
            </div>
        </div>
    </Field.Field>
{/if}
