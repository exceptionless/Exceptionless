<script lang="ts">
    import { browser } from '$app/environment';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { CodeBlock, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Select from '$comp/ui/select';
    import ExternalLink from '@lucide/svelte/icons/external-link';

    type McpClientId = 'cursor-mcp' | 'github-copilot-cli' | 'vs-code-mcp';

    type ActionLink = {
        href: string;
        label: string;
        variant?: 'default' | 'outline';
    };

    type CommandStep = {
        actions?: ActionLink[];
        code: string;
        description: string;
        language: 'json' | 'shellscript';
        title: string;
    };

    type McpClient = {
        description: string;
        id: McpClientId;
        links: ActionLink[];
        name: string;
        steps: CommandStep[];
    };

    let mcpEndpoint = $state('/mcp');
    let selectedClientId = $state<McpClientId>('vs-code-mcp');

    $effect(() => {
        if (browser) {
            mcpEndpoint = `${window.location.origin}/mcp`;
        }
    });

    const cursorConfiguration = $derived(`{
  "mcpServers": {
    "exceptionless": {
      "url": "${mcpEndpoint}"
    }
  }
}`);

    const cursorInstallUrl = $derived.by(() => {
        const serverConfiguration = {
            url: mcpEndpoint
        };

        const encodedConfiguration = btoa(JSON.stringify(serverConfiguration));

        return `cursor://anysphere.cursor-deeplink/mcp/install?name=exceptionless&config=${encodeURIComponent(encodedConfiguration)}`;
    });

    const githubCopilotCliConfiguration = $derived(`{
  "mcpServers": {
    "exceptionless": {
      "type": "http",
      "url": "${mcpEndpoint}",
      "tools": ["*"]
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
            description: 'Use any MCP-capable VS Code chat experience with the hosted Exceptionless MCP server.',
            id: 'vs-code-mcp',
            links: [
                {
                    href: 'https://code.visualstudio.com/docs/agent-customization/mcp-servers',
                    label: 'VS Code MCP docs'
                },
                {
                    href: 'https://code.visualstudio.com/',
                    label: 'Get VS Code'
                }
            ],
            name: 'VS Code MCP',
            steps: [
                {
                    actions: [
                        {
                            href: visualStudioCodeInstallUrl,
                            label: 'Add to VS Code',
                            variant: 'default'
                        }
                    ],
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
            description: 'Use Cursor with the hosted Exceptionless MCP server.',
            id: 'cursor-mcp',
            links: [
                {
                    href: 'https://cursor.com/docs/mcp',
                    label: 'Cursor MCP docs'
                },
                {
                    href: 'https://cursor.com/downloads',
                    label: 'Get Cursor'
                }
            ],
            name: 'Cursor MCP',
            steps: [
                {
                    actions: [
                        {
                            href: cursorInstallUrl,
                            label: 'Add to Cursor',
                            variant: 'default'
                        }
                    ],
                    code: cursorConfiguration,
                    description:
                        'Install the remote MCP server into Cursor. Use the button, or add this configuration to .cursor/mcp.json for a project setup or ~/.cursor/mcp.json for a user setup.',
                    language: 'json',
                    title: 'Add the server'
                },
                {
                    code: 'Cursor Settings > Tools & Integrations > MCP Tools',
                    description: 'Open Cursor MCP tools, enable Exceptionless, and approve the OAuth browser flow when prompted.',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        },
        {
            description: 'Use GitHub Copilot CLI with the hosted Exceptionless MCP server.',
            id: 'github-copilot-cli',
            links: [
                {
                    href: 'https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-mcp-servers',
                    label: 'Copilot CLI MCP docs'
                },
                {
                    href: 'https://docs.github.com/en/copilot/how-tos/copilot-cli/cli-getting-started',
                    label: 'Get Copilot CLI'
                }
            ],
            name: 'GitHub Copilot CLI',
            steps: [
                {
                    code: `copilot mcp add --transport http exceptionless ${mcpEndpoint}`,
                    description: 'Add the remote MCP server to GitHub Copilot CLI user configuration.',
                    language: 'shellscript',
                    title: 'Add the server'
                },
                {
                    code: githubCopilotCliConfiguration,
                    description: 'For manual setup, add this server entry to ~/.copilot/mcp-config.json.',
                    language: 'json',
                    title: 'Manual configuration'
                },
                {
                    code: 'copilot mcp list\ncopilot',
                    description: 'Confirm the server is listed, then start Copilot CLI and approve the OAuth browser flow when prompted.',
                    language: 'shellscript',
                    title: 'Authenticate'
                }
            ]
        }
    ]);

    const selectedClient = $derived(mcpClients.find((client) => client.id === selectedClientId) ?? mcpClients[0]!);
    const isWebUrl = (href: string) => href.startsWith('http://') || href.startsWith('https://');

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
                <div class="mt-3 flex flex-wrap gap-2">
                    {#each selectedClient.links as link (link.label)}
                        <Button
                            href={link.href}
                            variant={link.variant ?? 'outline'}
                            target={isWebUrl(link.href) ? '_blank' : undefined}
                            rel={isWebUrl(link.href) ? 'noreferrer' : undefined}
                        >
                            <ExternalLink data-icon="inline-start" />
                            {link.label}
                        </Button>
                    {/each}
                </div>
            </li>

            {#each selectedClient.steps as step (step.title)}
                <li class="pl-1">
                    <h5 class="text-sm font-medium">{step.title}</h5>
                    <P>{step.description}</P>
                    {#if step.actions?.length}
                        <div class="mt-2 flex flex-wrap gap-2">
                            {#each step.actions as action (action.label)}
                                <Button
                                    href={action.href}
                                    variant={action.variant ?? 'outline'}
                                    target={isWebUrl(action.href) ? '_blank' : undefined}
                                    rel={isWebUrl(action.href) ? 'noreferrer' : undefined}
                                >
                                    <ExternalLink data-icon="inline-start" />
                                    {action.label}
                                </Button>
                            {/each}
                        </div>
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
