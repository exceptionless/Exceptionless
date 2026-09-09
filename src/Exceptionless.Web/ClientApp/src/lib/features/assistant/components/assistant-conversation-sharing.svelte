<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Switch } from '$comp/ui/switch';

    import type { AssistantConversationSharingSettings } from '../models';

    interface Props {
        checked: boolean;
        error?: string;
        isSaving: boolean;
        onChange: (enabled: boolean | null) => Promise<void>;
        onRetry: () => Promise<void>;
        settings?: AssistantConversationSharingSettings;
    }

    let { checked, error, isSaving, onChange, onRetry, settings }: Props = $props();
    const id = $props.id();
</script>

<div class="space-y-1">
    <Field.Field orientation="horizontal" class="items-start gap-2">
        <Switch {id} {checked} disabled={!settings || isSaving} onCheckedChange={(value) => void onChange(value)} />
        <Field.Content class="gap-1">
            <Field.Label for={id} class="text-xs">Share conversations to improve Exie</Field.Label>
            <Field.Description class="text-xs">
                Allow Exceptionless to review your submitted messages and replies.
                {#if settings && !settings.is_overridden}Default: {settings.default_enabled ? 'on' : 'off'}. You can change this anytime.{/if}
            </Field.Description>
        </Field.Content>
        {#if settings?.is_overridden}
            <Button size="xs" variant="ghost" disabled={isSaving} onclick={() => void onChange(null)}>
                Use default ({settings.default_enabled ? 'on' : 'off'})
            </Button>
        {/if}
    </Field.Field>
    {#if error}
        <p class="text-destructive text-xs" role="alert">{error}</p>
        <Button size="xs" variant="outline" disabled={isSaving} onclick={() => void onRetry()}>Retry saving</Button>
    {/if}
</div>
