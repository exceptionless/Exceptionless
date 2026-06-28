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
    let selectedToolId = $state<AiToolId>('github-copilot');

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

    const visualStudioCodeConfiguration = $derived(`{
  "servers": {
    "exceptionless": {
      "type": "http",
      "url": "${mcpEndpoint}"
    }
  }
}`);

    const aiTools = $derived<AiTool[]>([
        {
            description: 'Use GitHub Copilot Chat in VS Code with the hosted Exceptionless MCP server.',
            id: 'github-copilot',
            name: 'GitHub Copilot',
            steps: [
                {
                    code: `code --add-mcp '{"name":"exceptionless","type":"http","url":"${mcpEndpoint}"}'`,
                    description: 'Add the remote MCP server to your VS Code user profile.',
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: visualStudioCodeConfiguration,
                    description: 'For a workspace-shared setup, add this server entry to .vscode/mcp.json instead.',
                    language: 'json',
                    title: 'Workspace configuration'
                },
                {
                    code: 'MCP: List Servers',
                    description: 'Open this VS Code command, select Exceptionless, and approve the OAuth browser flow when prompted.',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        },
        {
            description: 'Use Claude Code with the hosted Exceptionless MCP server.',
            id: 'claude',
            name: 'Claude Code',
            steps: [
                {
                    code: `claude mcp add --transport http exceptionless ${mcpEndpoint}`,
                    description: 'Add the hosted MCP server to Claude Code.',
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: 'claude mcp login exceptionless',
                    description: 'Start the OAuth browser flow and approve access.',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        },
        {
            description: 'Use Codex CLI with the hosted Exceptionless MCP server.',
            id: 'codex',
            name: 'Codex CLI',
            steps: [
                {
                    code: `codex mcp add exceptionless --url ${mcpEndpoint}`,
                    description: 'Register the streamable HTTP MCP server with Codex.',
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: 'codex mcp login exceptionless',
                    description: 'Start the OAuth browser flow and approve access.',
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
                    code: openCodeConfiguration,
                    description: 'Add this server entry to your opencode.json file.',
                    language: 'json',
                    title: 'Add the server configuration'
                },
                {
                    code: 'opencode mcp auth exceptionless',
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

<Card.Root>
    <Card.Header>
        <Card.Title class="text-sm font-medium">Setup Instructions</Card.Title>
        <Card.Description>Choose an AI tool, then follow the setup steps.</Card.Description>
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
