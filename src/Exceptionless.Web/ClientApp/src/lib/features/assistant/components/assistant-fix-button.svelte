<script lang="ts">
    import { Button } from '$comp/ui/button';
    import Bot from '@lucide/svelte/icons/bot';

    import { type AssistantFixResource, tryUseAssistantControls } from '../controls.svelte';

    interface Props {
        prepareContext: () => void;
        resource: AssistantFixResource;
    }

    let { prepareContext, resource }: Props = $props();
    const assistant = tryUseAssistantControls();

    const prompt = $derived(
        resource === 'stack'
            ? 'Analyze this stack and tell me how to fix the underlying issue. Identify the likely root cause and give me concrete, prioritized next steps.'
            : 'Analyze this event and tell me how to fix the underlying issue. Use the event and its stack context to identify the likely root cause and give me concrete, prioritized next steps.'
    );

    function askExie(): void {
        prepareContext();
        assistant?.ask(prompt);
    }
</script>

{#if assistant?.enabled()}
    <Button aria-label="Fix with Exie" data-assistant-trigger onclick={askExie} title="Fix with Exie" variant="outline">
        <Bot aria-hidden="true" />
        <span>Fix</span>
    </Button>
{/if}
