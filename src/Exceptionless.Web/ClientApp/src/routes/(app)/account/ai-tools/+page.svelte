<script lang="ts">
    import { browser } from '$app/environment';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { CodeBlock, Muted, P } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import * as Card from '$comp/ui/card';
    import * as Tabs from '$comp/ui/tabs';
    import Bot from '@lucide/svelte/icons/bot';
    import ShieldCheck from '@lucide/svelte/icons/shield-check';
    import Terminal from '@lucide/svelte/icons/terminal';

    type AiToolId = 'claude' | 'codex' | 'opencode';

    type CommandStep = {
        code: string;
        language: 'json' | 'shellscript';
        title: string;
    };

    type AiTool = {
        description: string;
        id: AiToolId;
        name: string;
        steps: CommandStep[];
    };

    let mcpEndpoint = $state('/mcp');
    let selectedToolId = $state<AiToolId>('claude');

    $effect(() => {
        if (browser) {
            mcpEndpoint = `${window.location.origin}/mcp`;
        }
    });

    const openCodeConfiguration = $derived(`{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "exceptionless": {
      "type": "remote",
      "url": "${mcpEndpoint}",
      "oauth": {}
    }
  }
}`);

    const aiTools = $derived<AiTool[]>([
        {
            description: 'Add the hosted MCP server, then sign in through the OAuth browser flow.',
            id: 'claude',
            name: 'Claude Code',
            steps: [
                {
                    code: `claude mcp add --transport http exceptionless ${mcpEndpoint}`,
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: 'claude mcp login exceptionless',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        },
        {
            description: 'Register the streamable HTTP server, then authenticate the saved server entry.',
            id: 'codex',
            name: 'Codex CLI',
            steps: [
                {
                    code: `codex mcp add exceptionless --url ${mcpEndpoint}`,
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: 'codex mcp login exceptionless',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        },
        {
            description: 'OpenCode configures remote MCP servers in opencode.json, then authenticates them from the CLI.',
            id: 'opencode',
            name: 'OpenCode',
            steps: [
                {
                    code: openCodeConfiguration,
                    language: 'json',
                    title: 'Add to opencode.json'
                },
                {
                    code: 'opencode mcp auth exceptionless',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        }
    ]);

    const selectedTool = $derived(aiTools.find((tool) => tool.id === selectedToolId) ?? aiTools[0]!);
</script>

<div class="space-y-6">
    <section class="space-y-2">
        <div class="flex flex-wrap items-center gap-2">
            <h4 class="text-base font-semibold">Exceptionless MCP Server</h4>
            <Badge variant="secondary" class="gap-1">
                <Bot class="size-3" aria-hidden="true" />
                OAuth
            </Badge>
        </div>
        <Muted>Connect AI tools to your Exceptionless projects so they can search projects, events, stacks, and perform approved actions.</Muted>
    </section>

    <Card.Root>
        <Card.Header class="gap-4">
            <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div class="space-y-1">
                    <Card.Title class="text-sm font-medium">Setup Instructions</Card.Title>
                    <Card.Description>Pick your AI tool, then run the commands shown below.</Card.Description>
                </div>

                <Tabs.Root bind:value={selectedToolId} class="shrink-0">
                    <Tabs.List class="w-full sm:w-auto">
                        {#each aiTools as tool (tool.id)}
                            <Tabs.Trigger value={tool.id} class="min-w-24 px-3">{tool.name}</Tabs.Trigger>
                        {/each}
                    </Tabs.List>
                </Tabs.Root>
            </div>
        </Card.Header>

        <Card.Content class="space-y-6">
            <div class="border-border bg-muted/30 flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
                <div class="min-w-0">
                    <div class="text-sm font-medium">MCP endpoint</div>
                    <code class="text-muted-foreground block truncate pt-1 text-sm">{mcpEndpoint}</code>
                </div>
                <CopyToClipboardButton value={mcpEndpoint} variant="outline" size="sm">Copy URL</CopyToClipboardButton>
            </div>

            <div class="grid gap-3 lg:grid-cols-2">
                <div class="border-border rounded-md border p-4">
                    <div class="flex items-center gap-2 text-sm font-medium">
                        <ShieldCheck class="size-4 text-green-600" aria-hidden="true" />
                        Permissions
                    </div>
                    <P class="text-muted-foreground mt-2 text-sm">
                        The client will request OAuth scopes during setup. Read tools can inspect your accessible data; write scopes allow stack updates such as
                        status changes, snoozing, critical events, and reference links.
                    </P>
                </div>
                <div class="border-border rounded-md border p-4">
                    <div class="flex items-center gap-2 text-sm font-medium">
                        <Terminal class="size-4 text-blue-600" aria-hidden="true" />
                        First use
                    </div>
                    <P class="text-muted-foreground mt-2 text-sm">
                        After authentication, ask your AI tool to use Exceptionless for questions like top issues, 404s, event details, or stack triage.
                    </P>
                </div>
            </div>

            <div class="border-border rounded-md border p-4">
                <div class="space-y-1">
                    <h5 class="text-sm font-semibold">{selectedTool.name}</h5>
                    <P class="text-muted-foreground text-sm">{selectedTool.description}</P>
                </div>

                <div class="mt-4 grid gap-4 lg:grid-cols-2">
                    {#each selectedTool.steps as step (step.title)}
                        <div class="space-y-2">
                            <div class="text-sm font-medium">{step.title}</div>
                            <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                                <CodeBlock code={step.code} language={step.language} class="max-h-80 overflow-auto pr-12" />
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value={step.code} variant="default" />
                                </div>
                            </div>
                        </div>
                    {/each}
                </div>
            </div>
        </Card.Content>
    </Card.Root>
</div>
