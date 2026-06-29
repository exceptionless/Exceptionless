<script lang="ts">
    import { browser } from '$app/environment';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { CodeBlock, Muted, P } from '$comp/typography';
    import * as Card from '$comp/ui/card';
    import * as Select from '$comp/ui/select';

    type AiToolId = 'claude' | 'codex' | 'github-copilot' | 'opencode';

    type CommandStep = {
        code: string;
        description: string;
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
    let mcpServerName = $state('exceptionless');
    let selectedToolId = $state<AiToolId>('claude');

    $effect(() => {
        if (browser) {
            mcpEndpoint = `${window.location.origin}/mcp`;
            mcpServerName = getMcpServerName(window.location.hostname);
        }
    });

    function getMcpServerName(hostname: string): string {
        return hostname.toLowerCase() === 'dev-app.exceptionless.io' ? 'exceptionless-dev' : 'exceptionless';
    }

    const aiTools = $derived<AiTool[]>([
        {
            description: 'Use Claude Code with the hosted Exceptionless MCP server.',
            id: 'claude',
            name: 'Claude Code',
            steps: [
                {
                    code: `claude mcp add --transport http ${mcpServerName} ${mcpEndpoint}`,
                    description: 'Add the hosted MCP server to Claude Code.',
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: `claude mcp login ${mcpServerName}`,
                    description: 'Start the OAuth browser flow and approve access.',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        },
        {
            description: 'Use Codex with the hosted Exceptionless MCP server.',
            id: 'codex',
            name: 'Codex',
            steps: [
                {
                    code: `codex mcp add ${mcpServerName} --url ${mcpEndpoint}`,
                    description: 'Register the streamable HTTP MCP server with Codex and approve access when prompted.',
                    language: 'shellscript',
                    title: 'Add and authenticate'
                }
            ]
        },
        {
            description: 'Use Copilot with the hosted Exceptionless MCP server.',
            id: 'github-copilot',
            name: 'Copilot',
            steps: [
                {
                    code: `copilot mcp add --transport http ${mcpServerName} ${mcpEndpoint}`,
                    description: 'Register the hosted HTTP MCP server with Copilot.',
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: 'copilot -i "List my Exceptionless projects"',
                    description: 'Start Copilot and approve the OAuth browser flow when prompted.',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        },
        {
            description: 'Use OpenCode with the hosted Exceptionless MCP server.',
            id: 'opencode',
            name: 'OpenCode',
            steps: [
                {
                    code: `opencode mcp add ${mcpServerName} --url ${mcpEndpoint}`,
                    description: 'Register the hosted HTTP MCP server with OpenCode.',
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: `opencode mcp auth ${mcpServerName}`,
                    description: 'Start the OAuth browser flow and approve access.',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        }
    ]);

    const selectedTool = $derived(aiTools.find((tool) => tool.id === selectedToolId) ?? aiTools[0]!);

    const examplePrompts = [
        'What are my top issues this week?',
        'Show me the top 404s in my project.',
        'What changed after version 1.0.2?',
        'Find recent errors for this user or order id.',
        'Mark this stack fixed in version 1.0.2.'
    ];
</script>

<div>
    <Card.Root>
        <Card.Header>
            <Card.Title class="text-sm font-medium">Setup Instructions</Card.Title>
            <Card.Description>Choose an AI tool, then run each command in order.</Card.Description>
        </Card.Header>

        <Card.Content class="space-y-6">
            <ol class="ml-6 list-decimal space-y-6">
                <li class="pl-1">
                    <P>Select your AI tool.</P>
                    <Select.Root
                        type="single"
                        value={selectedToolId}
                        onValueChange={(value) => {
                            selectedToolId = value as AiToolId;
                        }}
                    >
                        <Select.Trigger class="mt-2 w-full max-w-md">
                            <span>{selectedTool.name}</span>
                        </Select.Trigger>
                        <Select.Content>
                            {#each aiTools as tool (tool.id)}
                                <Select.Item value={tool.id}>{tool.name}</Select.Item>
                            {/each}
                        </Select.Content>
                    </Select.Root>
                    <Muted class="mt-2 block text-sm">{selectedTool.description}</Muted>
                </li>

                {#each selectedTool.steps as step (step.title)}
                    <li class="pl-1">
                        <P>{step.description}</P>
                        <div class="bg-muted relative mt-2 min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={step.code} language={step.language} class="max-h-80 overflow-auto pr-12" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={step.code} />
                            </div>
                        </div>
                    </li>
                {/each}
            </ol>

            <section class="border-border border-t pt-5">
                <h5 class="text-sm font-semibold">Try asking</h5>
                <ul class="mt-3 grid gap-2 sm:grid-cols-2">
                    {#each examplePrompts as prompt (prompt)}
                        <li class="bg-muted/40 rounded-md px-3 py-2 text-sm">{prompt}</li>
                    {/each}
                </ul>
            </section>
        </Card.Content>
    </Card.Root>
</div>
