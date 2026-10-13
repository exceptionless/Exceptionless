<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import * as Popover from '$comp/ui/popover';
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

<Popover.Root>
    <Popover.Trigger
        class={[
            'focus-visible:ring-ring shrink-0 cursor-pointer rounded-sm text-xs underline-offset-4 hover:underline focus-visible:ring-2 focus-visible:outline-none',
            error ? 'text-destructive' : 'text-muted-foreground hover:text-foreground'
        ]}
    >
        Chat sharing: {!settings ? 'Loading…' : checked ? 'On' : 'Off'}{error ? ' · Not saved' : ''}
    </Popover.Trigger>
    <Popover.Content side="top" align="end" sideOffset={8} class="flex w-80 flex-col gap-3 text-sm" aria-label="Conversation sharing">
        <Field.Field orientation="horizontal" class="items-center gap-3">
            <Field.Label for={id} class="flex-1">Share conversations to improve Exie</Field.Label>
            <Switch {id} {checked} disabled={!settings || isSaving} onCheckedChange={(value) => void onChange(value)} />
        </Field.Field>
        <p class="text-muted-foreground text-xs">Allow Exceptionless to review your messages and replies. Usage and error diagnostics stay enabled.</p>
        {#if settings && !settings.is_overridden}
            <p class="text-muted-foreground text-xs">Using the default: {settings.default_enabled ? 'on' : 'off'}. You can change this anytime.</p>
        {/if}
        {#if settings?.is_overridden}
            <Button size="xs" variant="outline" class="self-start" disabled={isSaving} onclick={() => void onChange(null)}>
                Use default ({settings.default_enabled ? 'on' : 'off'})
            </Button>
        {/if}
        {#if error}
            <p class="text-destructive text-xs" role="alert">{error}</p>
            <Button size="xs" variant="outline" class="self-start" disabled={isSaving} onclick={() => void onRetry()}>Retry saving</Button>
        {/if}
    </Popover.Content>
</Popover.Root>
