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

/** The hosted REST API, for requests sent without a client. */
const DEFAULT_SUBMISSION_URL = 'https://api.exceptionless.io';

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
    const context: PlatformSetupContext = { apiKey: 'YOUR_API_KEY', sdkServerUrl: 'YOUR_SERVER_URL', submissionServerUrl: 'YOUR_SERVER_URL' };

    return [
        '# Set up Exceptionless',
        'Instructions for a coding agent adding Exceptionless error and event tracking to an app.',
        '## Before you start',
        [
            "- Replace `YOUR_API_KEY` with the API key the user gave you. If they didn't give one, ask for it. It's on the project's API Keys page in Exceptionless.",
            `- Replace \`YOUR_SERVER_URL\` with the server URL the user gave you. If they didn't give one, leave out the lines that set the server URL, and send REST API requests to \`${DEFAULT_SUBMISSION_URL}\`.`,
            '- A client API key can only send events, read client configuration, and connect the project to Zapier, so it is fine in shipped client code. Prefer configuration or environment variables so the key can be rotated. Never put a user or OAuth token in client code.',
            '- JavaScript clients queue events until `Exceptionless.startup()` finishes, so there is no need to await it.',
            "- A `localhost` server URL only works on the user's own machine. Phones and emulators need the machine's network address instead (`10.0.2.2` on the Android emulator)."
        ].join('\n'),
        "## If you can't see the user's code",
        [
            "If you're a chat assistant that can't read or edit the user's files:",
            '',
            '1. Ask which language and framework the app uses, and to paste its entry file if they want exact placement.',
            '2. In the same reply, give them the REST API (curl) or PowerShell example with their key and server filled in, so they can check both right away. A 202 response means it worked.',
            "3. Once you know the stack, give only that section's steps with their key and server filled in.",
            '4. Tell them to run the app once, look for "Hello from Exceptionless setup" in their Exceptionless project, then remove the test error.'
        ].join('\n'),
        '## Steps',
        [
            `1. Work out the app's language and framework from its code, and find its section below: ${PLATFORMS.map((platform) => platform.label).join(', ')}, or Other languages.`,
            '2. Follow its required steps, adapting file names and structure to this project. Do an Optional step when it fits the app, and mention the others to the user.',
            `3. Run the app so it sends the test error "${TEST_ERROR_MESSAGE}". In browser and mobile apps, put the test error in the entry file right after startup, not in a component. Clients send queued events within about 10 seconds, and on a graceful exit.`,
            "4. Check it worked with the section's \"Check it worked\" step, and wait for that before stopping servers, workers, and browser pages (stop them with Ctrl+C or SIGINT). The API key can't read events back, so a 403 when you try is expected. If you can't run the app or a browser, check the key and server with the REST API example and ask the user to run the app.",
            '5. Remove the test error, the logging you added to check it, and any log file it wrote. Keep everything else.',
            '6. Tell the user what you changed, where the API key lives, and that the test error is waiting for them in Exceptionless.'
        ].join('\n'),
        ...PLATFORMS.map((platform) => formatPlatform(platform, context)),
        '## Other languages',
        [
            'There are official clients only for .NET and JavaScript. Packages named "exceptionless" in other registries are not ours, so don\'t install them. Report errors through the REST API instead:',
            '',
            "1. Hook the framework's error handler (for example Flask or Django `got_request_exception`, FastAPI `add_exception_handler`, Go `recover` middleware, Rails `rescue_from`). POST each unhandled error to `YOUR_SERVER_URL/api/v2/events` with the headers `Authorization: Bearer YOUR_API_KEY` and `Content-Type: application/json`.",
            "2. Send a body like the REST API example. In `@simple_error`, use the error's message, its class name as the type (for example `ValueError`), and the language's formatted stack trace. Set `environment` from the app's environment name, or an `ENVIRONMENT` variable that defaults to `production`.",
            '3. Send in the background with a short timeout, let shutdown wait for a send in progress, and never let a failed send break the app.',
            '4. Send the test error through that handler, for example from a temporary route that raises it.',
            '',
            '### Check it worked',
            '',
            'Temporarily log the response status. A 202 means the event was accepted. Remove the log line and the temporary route afterwards.'
        ].join('\n')
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
        ...steps.filter((step) => step.optional).map((step) => formatStep(`Optional: ${step.title}`, step)),
        `### Check it worked\n\n${platform.debugLogging}`
    ].join('\n\n');
}

function formatStep(heading: string, step: PlatformStep): string {
    return [
        `### ${heading}`,
        ...(step.description ? [step.description] : []),
        ...(step.blocks ?? []).map((block) => `\`\`\`${FENCE_LANGUAGES[block.language]}\n${block.code}\n\`\`\``)
    ].join('\n\n');
}
