<script lang="ts">
    import { browser } from '$app/env';
    import { resolve } from '$app/paths';

    import CopyToClipboardButton from '#comp/copy-to-clipboard-button.svelte';
    import { CodeBlock, Muted, P } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import * as Select from '#comp/ui/select/index.ts';
    import { getServerUrl } from '#shared/api/urls.ts';
    import { createQueryParameters } from '#shared/query-params/index.ts';

    type AiTool = {
        description: string;
        id: AiToolId;
        name: string;
        steps: SetupStep[];
    };

    type AiToolId = NonNullable<typeof queryParams.type>;

    type SetupStep = ({ code: string; language: 'json' | 'shellscript' } | { code?: never; language?: never }) & {
        description: string;
        installUrl?: string;
        title: string;
    };

    let mcpEndpoint = $state('/mcp');
    let mcpServerName = $state('exceptionless');

    const queryParams = createQueryParameters({
        defaults: {
            type: 'claude'
        },
        history: 'push',
        schema: {
            type: '<claude,codex,cursor,github-copilot,opencode>'
        }
    });

    const cursorInstallUrl = $derived(
        browser && mcpEndpoint !== '/mcp'
            ? `cursor://anysphere.cursor-deeplink/mcp/install?${new URLSearchParams({
                  config: btoa(
                      JSON.stringify({
                          url: mcpEndpoint
                      })
                  ),
                  name: mcpServerName
              })}`
            : undefined
    );

    $effect(() => {
        if (browser) {
            const endpoint = new URL(getServerUrl('mcp'), window.location.origin);
            mcpEndpoint = endpoint.href;
            mcpServerName = getMcpServerName(endpoint.hostname);
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
            description: 'Use Cursor with the hosted Exceptionless MCP server.',
            id: 'cursor',
            name: 'Cursor',
            steps: [
                {
                    code: JSON.stringify(
                        {
                            mcpServers: {
                                [mcpServerName]: {
                                    url: mcpEndpoint
                                }
                            }
                        },
                        null,
                        4
                    ),
                    description:
                        'Add the server to Cursor, or add this configuration to ~/.cursor/mcp.json (or .cursor/mcp.json in your project). Keep any existing servers.',
                    installUrl: cursorInstallUrl,
                    language: 'json',
                    title: 'Add the server'
                },
                {
                    description: 'Connect to the server in Cursor and approve the OAuth sign-in when prompted.',
                    title: 'Authenticate'
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

    const selectedTool = $derived(aiTools.find((tool) => tool.id === queryParams.type) ?? aiTools[0]!);
    const toolLink = $derived(browser ? new URL(`${resolve('/(app)/account/ai-tools')}?type=${selectedTool.id}`, window.location.origin).href : '');

    const examplePrompts = [
        'What are my top issues this week?',
        'Fix my top error',
        'Show me the top 404s in my project',
        'What changed after version 1.0.2?',
        'Find recent errors for this user or order id',
        'Mark this stack fixed in version 1.0.2'
    ];
</script>

<div>
    <Card.Root>
        <Card.Header>
            <Card.Title class="text-sm font-medium">Setup Instructions</Card.Title>
            <Card.Description>Choose an AI tool, then follow the setup steps.</Card.Description>
        </Card.Header>

        <Card.Content class="space-y-6">
            <ol class="ml-6 list-decimal space-y-6">
                <li class="pl-1">
                    <P>Select your AI tool.</P>
                    <div class="mt-2 flex flex-wrap items-center gap-2">
                        <Select.Root
                            type="single"
                            value={selectedTool.id}
                            onValueChange={(value) => {
                                queryParams.type = value as AiToolId;
                            }}
                        >
                            <Select.Trigger aria-label="AI tool" class="w-full max-w-md">
                                <span>{selectedTool.name}</span>
                            </Select.Trigger>
                            <Select.Content>
                                {#each aiTools as tool (tool.id)}
                                    <Select.Item value={tool.id}>{tool.name}</Select.Item>
                                {/each}
                            </Select.Content>
                        </Select.Root>
                        <CopyToClipboardButton title={`Copy link to ${selectedTool.name} setup`} size="default" variant="outline" value={toolLink}>
                            Copy link
                        </CopyToClipboardButton>
                    </div>
                    <Muted class="mt-2 block text-sm">{selectedTool.description}</Muted>
                </li>

                {#each selectedTool.steps as step (step.title)}
                    <li class="pl-1">
                        <P>{step.description}</P>
                        {#if step.installUrl}
                            <Button href={step.installUrl} class="mt-2">Add to Cursor</Button>
                        {/if}
                        {#if step.code && step.language}
                            <div class="bg-muted relative mt-2 min-h-13 overflow-hidden rounded-md">
                                <CodeBlock code={step.code} language={step.language} class="max-h-80 overflow-auto pr-12" />
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value={step.code} />
                                </div>
                            </div>
                        {/if}
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
