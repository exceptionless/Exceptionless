import { type CodeLanguage, type Platform, PLATFORMS, type PlatformSetupContext, type PlatformStep, TEST_ERROR_MESSAGE } from './platforms';

/** Coding agents and assistants that can open with the setup prompt already filled in. */
export type AgentTarget = 'chatgpt' | 'claude' | 'cursor';

export interface AgentTargetOption {
    id: AgentTarget;
    label: string;
}

export const AGENT_TARGETS: readonly AgentTargetOption[] = [
    { id: 'cursor', label: 'Cursor' },
    { id: 'claude', label: 'Claude' },
    { id: 'chatgpt', label: 'ChatGPT' }
];

/** Published from docs/public/setup.md.txt on the website, so hosted agents can read it even for self-hosted installs. */
export const AGENT_SETUP_URL = 'https://exceptionless.com/setup.md';

/** The hosted collector the clients use by default. */
const DEFAULT_SUBMISSION_URL = 'https://collector.exceptionless.io';

const FENCE_LANGUAGES: Record<CodeLanguage, string> = {
    csharp: 'csharp',
    javascript: 'javascript',
    json: 'json',
    powershell: 'powershell',
    shellscript: 'bash',
    xml: 'xml'
};

/** The instructions published at {@link AGENT_SETUP_URL}, built from the same steps the setup page shows. */
export function buildAgentSetupMarkdown(): string {
    const context: PlatformSetupContext = { apiKey: 'YOUR_API_KEY', submissionServerUrl: DEFAULT_SUBMISSION_URL };

    return [
        '# Set up Exceptionless',
        'Instructions for a coding agent adding Exceptionless error and event tracking to an app.',
        '## Before you start',
        [
            "- Use the API key the user gave you in place of `YOUR_API_KEY`. If they didn't give one, ask for it. It's on the project's API Keys page in Exceptionless.",
            "- If the user gave you a server URL, they run their own Exceptionless server. Set the client's server URL to it (`c.ServerUrl` in .NET, `c.serverUrl` in JavaScript), and send REST API requests to it instead of " +
                `\`${DEFAULT_SUBMISSION_URL}\`. Otherwise keep the client's default server.`,
            '- If the project already keeps settings like this in configuration or environment variables, put the API key there.'
        ].join('\n'),
        '## Steps',
        [
            "1. Work out the app's language and framework, and find the matching section below. For a platform that isn't listed, use REST API (curl).",
            '2. Follow its steps, adapting file names and structure to this project.',
            `3. Run the app once so the test error "${TEST_ERROR_MESSAGE}" is sent. Confirm it was accepted, then remove the test code.`,
            '4. Tell the user what you changed. The test event appears in Exceptionless within a few seconds.'
        ].join('\n'),
        ...PLATFORMS.map((platform) => formatPlatform(platform, context))
    ].join('\n\n');
}

/** A short prompt that points the agent at the published instructions and gives it this project's settings. */
export function buildAgentSetupPrompt(context: PlatformSetupContext, platform?: Platform): string {
    const app = platform ? ` in this ${platform.label} app` : '';
    const server = context.sdkServerUrl ? ` My Exceptionless server is ${context.sdkServerUrl}.` : '';
    return `Fetch and follow the instructions at ${AGENT_SETUP_URL} to set up Exceptionless for me${app}. My API key is ${context.apiKey}.${server}`;
}

/** Opens the agent with the prompt filled in. Spaces are %20, which every reader of the query decodes; some leave `+` as is. */
export function getAgentUrl(target: AgentTarget, prompt: string): string {
    const text = encodeURIComponent(prompt);
    switch (target) {
        case 'chatgpt':
            return `https://chatgpt.com/?q=${text}`;
        case 'claude':
            return `https://claude.ai/new?q=${text}`;
        case 'cursor':
            return `https://cursor.com/link/prompt?text=${text}`;
    }
}

function formatPlatform(platform: Platform, context: PlatformSetupContext): string {
    const steps = platform.steps(context);
    return [
        `## ${platform.label}`,
        `${platform.category}. Docs: ${platform.docsUrl}`,
        ...steps.filter((step) => !step.optional).map((step, index) => formatStep(`${index + 1}. ${step.title}`, step)),
        ...steps.filter((step) => step.optional).map((step) => formatStep(`Optional: ${step.title}`, step))
    ].join('\n\n');
}

function formatStep(heading: string, step: PlatformStep): string {
    return [
        `### ${heading}`,
        ...(step.description ? [step.description] : []),
        ...(step.blocks ?? []).map((block) => `\`\`\`${FENCE_LANGUAGES[block.language]}\n${block.code}\n\`\`\``)
    ].join('\n\n');
}
