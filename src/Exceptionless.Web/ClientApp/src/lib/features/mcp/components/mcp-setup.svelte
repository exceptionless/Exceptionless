<script lang="ts">
    import { browser } from '$app/environment';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { CodeBlock, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Select from '$comp/ui/select';
    import ExternalLink from '@lucide/svelte/icons/external-link';

    type McpClientId = 'claude' | 'codex' | 'github-copilot-vscode' | 'opencode';

    type CommandStep = {
        action?: {
            href: string;
            label: string;
        };
        code: string;
        description: string;
        language: 'json' | 'shellscript';
        title: string;
    };

    type McpClient = {
        description: string;
        id: McpClientId;
        name: string;
        steps: CommandStep[];
    };

    let mcpEndpoint = $state('/mcp');
    let selectedClientId = $state<McpClientId>('github-copilot-vscode');

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

    const visualStudioCodeInstallUrl = $derived.by(() => {
        const serverConfiguration = {
            name: 'exceptionless',
            type: 'http',
            url: mcpEndpoint
        };

        return `vscode:mcp/install?${encodeURIComponent(JSON.stringify(serverConfiguration))}`;
    });

    const mcpClients = $derived<McpClient[]>([
        {
            description: 'Use GitHub Copilot Chat in VS Code with the hosted Exceptionless MCP server.',
            id: 'github-copilot-vscode',
            name: 'GitHub Copilot in VS Code',
            steps: [
                {
                    action: {
                        href: visualStudioCodeInstallUrl,
                        label: 'Add to VS Code'
                    },
                    code: `code --add-mcp '{"name":"exceptionless","type":"http","url":"${mcpEndpoint}"}'`,
                    description:
                        'Add the remote MCP server to your VS Code user profile. Use the button, or copy the command if your browser blocks the VS Code prompt.',
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

    const selectedClient = $derived(mcpClients.find((client) => client.id === selectedClientId) ?? mcpClients[0]!);

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
        <Card.Title class="text-sm font-medium">MCP Setup</Card.Title>
        <Card.Description>Choose an MCP client, then follow the setup steps.</Card.Description>
    </Card.Header>

    <Card.Content class="flex flex-col gap-6">
        <ol class="ml-6 flex list-decimal flex-col gap-6">
            <li class="pl-1">
                <P>Select your MCP client.</P>
                <Select.Root
                    type="single"
                    value={selectedClientId}
                    onValueChange={(value) => {
                        selectedClientId = value as McpClientId;
                    }}
                >
                    <Select.Trigger class="mt-2 w-full max-w-md">
                        <span>{selectedClient.name}</span>
                    </Select.Trigger>
                    <Select.Content>
                        <Select.Group>
                            {#each mcpClients as client (client.id)}
                                <Select.Item value={client.id}>{client.name}</Select.Item>
                            {/each}
                        </Select.Group>
                    </Select.Content>
                </Select.Root>
                <Muted class="mt-2 block text-sm">{selectedClient.description}</Muted>
            </li>

            {#each selectedClient.steps as step (step.title)}
                <li class="pl-1">
                    <P>{step.description}</P>
                    {#if step.action}
                        <Button href={step.action.href} variant="outline" class="mt-2">
                            <ExternalLink data-icon="inline-start" />
                            {step.action.label}
                        </Button>
                    {/if}
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
