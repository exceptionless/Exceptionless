/**
 * Client setup instructions for each supported platform.
 *
 * Snippets are checked against the published clients (Exceptionless.Net 6.2 and the
 * @exceptionless/* 3.2 npm packages). Keep every historical `?type=` value resolvable:
 * links to `/project/{id}/configure?type=...` are shared in docs, emails, and bookmarks.
 */

export type CodeLanguage = 'csharp' | 'javascript' | 'json' | 'powershell' | 'shellscript' | 'xml';

export interface Platform {
    /** Previous `?type=` values that should open this platform. */
    aliases?: readonly string[];
    category: PlatformCategory;
    /** How to turn on the client's own diagnostic logging. */
    debugLogging: string;
    docsUrl: string;
    group: 'legacy' | 'popular';
    icon: PlatformIcon;
    /** Canonical `?type=` value. */
    id: string;
    /** Browser clients can be blocked by ad and privacy blockers. */
    isBrowser?: boolean;
    /** Raw HTTP examples send one event and do not install a client. */
    isRawHttp?: boolean;
    keywords: readonly string[];
    label: string;
    steps: (context: PlatformSetupContext) => PlatformStep[];
    /** One-line hint from Exie after the platform is picked. */
    tip: string;
}

export type PlatformCategory = '.NET' | '.NET Framework' | 'Command line' | 'JavaScript' | 'Mobile';

export interface PlatformCodeBlock {
    code: string;
    language: CodeLanguage;
}

export type PlatformIcon =
    | 'app-window'
    | 'atom'
    | 'braces'
    | 'cog'
    | 'globe'
    | 'hexagon'
    | 'panels-top-left'
    | 'server'
    | 'shield'
    | 'smartphone'
    | 'square-chevron-right'
    | 'square-terminal'
    | 'tablet-smartphone'
    | 'triangle';

export interface PlatformSetupContext {
    apiKey: string;
    /** Absolute API origin shown in SDK snippets. Omitted when the client default (hosted) applies. */
    sdkServerUrl?: string;
    /** Absolute API origin for raw HTTP snippets. Always present. */
    submissionServerUrl: string;
}

export interface PlatformStep {
    blocks?: PlatformCodeBlock[];
    description?: string;
    /** Optional extras are shown after the required steps and labeled as optional. */
    optional?: boolean;
    title: string;
}

export const TEST_ERROR_MESSAGE = 'Hello from Exceptionless setup';

const DOTNET_DOCS_URL = 'https://exceptionless.com/docs/clients/dotnet/';
const JAVASCRIPT_DOCS_URL = 'https://exceptionless.com/docs/clients/javascript/';
const REACT_NATIVE_DOCS_URL = 'https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/packages/react-native';
const API_DOCS_URL = 'https://exceptionless.com/docs/api/';

const DOTNET_DEBUG_LOGGING =
    'Add c.UseFileLogger("exceptionless.log", Exceptionless.Logging.LogLevel.Debug) to your Exceptionless configuration, then check exceptionless.log for submission errors.';
const DOTNET_FRAMEWORK_DEBUG_LOGGING =
    'Add enableLogging="true" logPath="exceptionless.log" to the <exceptionless> element (or call ExceptionlessClient.Default.Configuration.UseFileLogger("exceptionless.log")), then check the log for submission errors.';
const JAVASCRIPT_DEBUG_LOGGING = 'Call c.useDebugLogger() inside Exceptionless.startup to log what the client is doing to the console.';
const RAW_HTTP_DEBUG_LOGGING = 'A 202 Accepted response means the event was queued. A 401 means the API key is wrong or disabled.';

const DOTNET_TEST_ERROR = `new Exception("${TEST_ERROR_MESSAGE}").ToExceptionless().Submit();`;
const JAVASCRIPT_TEST_ERROR = `Exceptionless.submitException(new Error("${TEST_ERROR_MESSAGE}"));`;

function desktopDotnetSteps(packageName: string, entryPoint: 'App.xaml.cs' | 'Program.cs', context: PlatformSetupContext): PlatformStep[] {
    const startup =
        entryPoint === 'App.xaml.cs'
            ? `using Exceptionless;

public partial class App : Application {
    private void Application_Startup(object sender, StartupEventArgs e) {
${dotnetDefaultClientConfiguration(context, '        ')}
        ExceptionlessClient.Default.Register();
    }
}`
            : `using Exceptionless;

internal static class Program {
    [STAThread]
    private static void Main() {
${dotnetDefaultClientConfiguration(context, '        ')}
        ExceptionlessClient.Default.Register();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}`;

    return [
        dotnetInstall(packageName),
        {
            blocks: [{ code: startup, language: 'csharp' }],
            description:
                entryPoint === 'App.xaml.cs'
                    ? 'Register Exceptionless when your app starts. Point Startup="Application_Startup" at this handler in App.xaml.'
                    : 'Register Exceptionless before Application.Run so unhandled exceptions are reported.',
            title: `Register Exceptionless in ${entryPoint}`
        },
        dotnetTestError('Add this after Register() and run your app once.')
    ];
}

/** `c => { ... }` configuration lambda shared by the .NET hosting integrations. */
function dotnetConfigureLambda({ apiKey, sdkServerUrl }: PlatformSetupContext, indent = '    '): string {
    const lines = [`${indent}c.ApiKey = "${apiKey}";`];
    if (sdkServerUrl) {
        lines.push(`${indent}c.ServerUrl = "${sdkServerUrl}";`);
    }

    return `c => {\n${lines.join('\n')}\n}`;
}

function dotnetDefaultClientConfiguration({ apiKey, sdkServerUrl }: PlatformSetupContext, indent = ''): string {
    const lines = [`${indent}ExceptionlessClient.Default.Configuration.ApiKey = "${apiKey}";`];
    if (sdkServerUrl) {
        lines.push(`${indent}ExceptionlessClient.Default.Configuration.ServerUrl = "${sdkServerUrl}";`);
    }

    return lines.join('\n');
}

function dotnetInstall(packageName: string): PlatformStep {
    return {
        blocks: [{ code: `dotnet add package ${packageName}`, language: 'shellscript' }],
        description: 'Run this from your project folder.',
        title: `Install ${packageName}`
    };
}

function dotnetTestError(description: string): PlatformStep {
    return {
        blocks: [{ code: DOTNET_TEST_ERROR, language: 'csharp' }],
        description,
        title: 'Send a test error'
    };
}

function javascriptStartup({ apiKey, sdkServerUrl }: PlatformSetupContext, indent = ''): string {
    const lines = [`${indent}  c.apiKey = "${apiKey}";`];
    if (sdkServerUrl) {
        lines.push(`${indent}  c.serverUrl = "${sdkServerUrl}";`);
    }

    return `${indent}Exceptionless.startup((c) => {\n${lines.join('\n')}\n${indent}});`;
}

function legacyConfigSection({ apiKey, sdkServerUrl }: PlatformSetupContext): string {
    const serverUrlAttribute = sdkServerUrl ? ` serverUrl="${sdkServerUrl}"` : '';
    return `<configSections>
  <section name="exceptionless" type="Exceptionless.ExceptionlessSection, Exceptionless" />
</configSections>

<exceptionless apiKey="${apiKey}"${serverUrlAttribute} />`;
}

function legacyConfigStep(fileName: 'app.config' | 'web.config', context: PlatformSetupContext): PlatformStep {
    return {
        blocks: [{ code: legacyConfigSection(context), language: 'xml' }],
        description: `NuGet adds this to ${fileName} for packages.config projects. Check it is there with your API key; PackageReference projects need it added by hand.`,
        title: `Add your API key to ${fileName}`
    };
}

function legacyInstall(packageName: string): PlatformStep {
    return {
        blocks: [{ code: `Install-Package ${packageName}`, language: 'powershell' }],
        description: 'Run this in the Visual Studio Package Manager Console.',
        title: `Install ${packageName}`
    };
}

function legacyModuleStep(moduleType: string): PlatformStep {
    return {
        blocks: [
            {
                code: `<system.webServer>
  <modules>
    <remove name="ExceptionlessModule" />
    <add name="ExceptionlessModule" type="${moduleType}" />
  </modules>
</system.webServer>`,
                language: 'xml'
            }
        ],
        description: 'NuGet usually registers this module in web.config for you. It starts the client when your app starts.',
        title: 'Check the Exceptionless module'
    };
}

function npmInstall(packages: string): PlatformStep {
    return {
        blocks: [{ code: `npm install ${packages}`, language: 'shellscript' }],
        description: 'Run this from your project folder.',
        title: 'Install the client'
    };
}

const platforms: readonly Platform[] = [
    {
        category: '.NET',
        debugLogging: DOTNET_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'popular',
        icon: 'server',
        id: 'dotnet-aspnetcore',
        keywords: ['asp.net', 'aspnet', 'core', 'csharp', 'c#', 'dotnet', 'web api', 'mvc', 'blazor', 'minimal api'],
        label: 'ASP.NET Core',
        steps: (context) => [
            dotnetInstall('Exceptionless.AspNetCore'),
            {
                blocks: [
                    {
                        code: `using Exceptionless;

var builder = WebApplication.CreateBuilder(args);
builder.AddExceptionless(${dotnetConfigureLambda(context)});
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseExceptionless();`,
                        language: 'csharp'
                    }
                ],
                description: 'Register the client and middleware in Program.cs. Settings in appsettings.json under "Exceptionless" work too.',
                title: 'Add Exceptionless to Program.cs'
            },
            dotnetTestError('Add this after app.UseExceptionless(), run your app once, then remove it.'),
            {
                blocks: [
                    { code: 'dotnet add package Exceptionless.Extensions.Logging', language: 'shellscript' },
                    { code: 'builder.Logging.AddExceptionless();', language: 'csharp' }
                ],
                description: 'Send warnings and errors from ILogger too. You can tune log levels per source later from project settings.',
                optional: true,
                title: 'Send your logs'
            }
        ],
        tip: 'Once the middleware is in, unhandled exceptions and 404s show up on their own.'
    },
    {
        category: '.NET',
        debugLogging: DOTNET_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'popular',
        icon: 'square-terminal',
        id: 'dotnet-console',
        keywords: ['console', 'cli', 'csharp', 'c#', 'dotnet', 'service', 'library'],
        label: 'Console app (.NET)',
        steps: (context) => [
            dotnetInstall('Exceptionless'),
            {
                blocks: [
                    {
                        code: `using Exceptionless;
${context.sdkServerUrl ? `\nExceptionlessClient.Default.Configuration.ServerUrl = "${context.sdkServerUrl}";` : ''}
ExceptionlessClient.Default.Startup("${context.apiKey}");

${DOTNET_TEST_ERROR}

// Console apps exit quickly, so send anything queued before exiting.
await ExceptionlessClient.Default.ProcessQueueAsync();`,
                        language: 'csharp'
                    }
                ],
                description: 'Start the client when your app starts. The second line sends a test error so you can see it arrive.',
                title: 'Start Exceptionless and send a test error'
            }
        ],
        tip: 'Console apps can exit before the queue sends, so keep that ProcessQueueAsync line at the end.'
    },
    {
        category: '.NET',
        debugLogging: DOTNET_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'popular',
        icon: 'cog',
        id: 'dotnet-worker',
        keywords: ['worker', 'service', 'hosted', 'generic host', 'background', 'daemon', 'csharp', 'c#', 'dotnet'],
        label: 'Worker Service (.NET)',
        steps: (context) => [
            dotnetInstall('Exceptionless.Extensions.Hosting'),
            {
                blocks: [
                    {
                        code: `using Exceptionless;

var builder = Host.CreateApplicationBuilder(args);
builder.AddExceptionless(${dotnetConfigureLambda(context)});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();`,
                        language: 'csharp'
                    }
                ],
                description: 'Register the client in Program.cs. It flushes queued events when the host stops.',
                title: 'Add Exceptionless to Program.cs'
            },
            {
                blocks: [
                    {
                        code: `host.Services.GetRequiredService<ExceptionlessClient>()
    .SubmitException(new Exception("${TEST_ERROR_MESSAGE}"));`,
                        language: 'csharp'
                    }
                ],
                description: 'Add this just before host.Run(), run your worker once, then remove it.',
                title: 'Send a test error'
            },
            {
                blocks: [
                    { code: 'dotnet add package Exceptionless.Extensions.Logging', language: 'shellscript' },
                    { code: 'builder.Logging.AddExceptionless();', language: 'csharp' }
                ],
                description: 'Send warnings and errors from ILogger too.',
                optional: true,
                title: 'Send your logs'
            }
        ],
        tip: 'The hosting package hooks unhandled and unobserved task exceptions while the host runs.'
    },
    {
        category: 'JavaScript',
        debugLogging: JAVASCRIPT_DEBUG_LOGGING,
        docsUrl: JAVASCRIPT_DOCS_URL,
        group: 'popular',
        icon: 'globe',
        id: 'javascript-browser',
        isBrowser: true,
        keywords: ['browser', 'javascript', 'typescript', 'js', 'ts', 'web', 'spa', 'svelte', 'sveltekit', 'vite', 'next', 'nextjs', 'frontend'],
        label: 'Browser JavaScript',
        steps: (context) => [
            npmInstall('@exceptionless/browser'),
            {
                blocks: [
                    {
                        code: `import { Exceptionless } from "@exceptionless/browser";

${javascriptStartup(context)}`,
                        language: 'javascript'
                    }
                ],
                description: 'Run this once in your app entry point, before the rest of your code.',
                title: 'Start Exceptionless'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup, for example from your browser console or a button.',
                title: 'Send a test error'
            }
        ],
        tip: 'Unhandled errors and promise rejections are captured automatically after startup.'
    },
    {
        category: 'JavaScript',
        debugLogging: JAVASCRIPT_DEBUG_LOGGING,
        docsUrl: JAVASCRIPT_DOCS_URL,
        group: 'popular',
        icon: 'hexagon',
        id: 'javascript-nodejs',
        keywords: ['node', 'nodejs', 'express', 'fastify', 'nest', 'server', 'javascript', 'typescript', 'js', 'ts', 'bun', 'deno'],
        label: 'Node.js',
        steps: (context) => [
            npmInstall('@exceptionless/node'),
            {
                blocks: [
                    {
                        code: `import { Exceptionless } from "@exceptionless/node";

${javascriptStartup(context)}`,
                        language: 'javascript'
                    }
                ],
                description: 'Run this once when your process starts, before the rest of your code.',
                title: 'Start Exceptionless'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup. Queued events are sent before the process exits.',
                title: 'Send a test error'
            }
        ],
        tip: 'Uncaught exceptions and unhandled rejections are captured, and the queue flushes before exit.'
    },
    {
        category: 'JavaScript',
        debugLogging: JAVASCRIPT_DEBUG_LOGGING,
        docsUrl: JAVASCRIPT_DOCS_URL,
        group: 'popular',
        icon: 'atom',
        id: 'javascript-react',
        isBrowser: true,
        keywords: ['react', 'jsx', 'tsx', 'next', 'nextjs', 'vite', 'remix', 'javascript', 'typescript', 'js', 'ts'],
        label: 'React',
        steps: (context) => [
            npmInstall('@exceptionless/react'),
            {
                blocks: [
                    {
                        code: `import { Exceptionless, ExceptionlessErrorBoundary } from "@exceptionless/react";

${javascriptStartup(context)}

root.render(
  <ExceptionlessErrorBoundary fallback={<p>Something went wrong.</p>}>
    <App />
  </ExceptionlessErrorBoundary>
);`,
                        language: 'javascript'
                    }
                ],
                description: 'Start the client in your entry file and wrap your app in the error boundary to catch render errors.',
                title: 'Start Exceptionless and add the error boundary'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup, for example from a button click.',
                title: 'Send a test error'
            }
        ],
        tip: 'The error boundary reports render errors; everything else is caught globally.'
    },
    {
        category: 'JavaScript',
        debugLogging: JAVASCRIPT_DEBUG_LOGGING,
        docsUrl: JAVASCRIPT_DOCS_URL,
        group: 'popular',
        icon: 'triangle',
        id: 'javascript-vue',
        isBrowser: true,
        keywords: ['vue', 'vuejs', 'nuxt', 'vite', 'javascript', 'typescript', 'js', 'ts'],
        label: 'Vue',
        steps: (context) => [
            npmInstall('@exceptionless/vue'),
            {
                blocks: [
                    {
                        code: `import { createApp } from "vue";
import { Exceptionless, ExceptionlessErrorHandler } from "@exceptionless/vue";
import App from "./App.vue";

${javascriptStartup(context)}

const app = createApp(App);
app.config.errorHandler = ExceptionlessErrorHandler;
app.mount("#app");`,
                        language: 'javascript'
                    }
                ],
                description: 'Start the client in main.js and hand component errors to Exceptionless.',
                title: 'Start Exceptionless and add the error handler'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup, for example from a button click.',
                title: 'Send a test error'
            }
        ],
        tip: 'ExceptionlessErrorHandler reports component errors with the component info attached.'
    },
    {
        category: 'JavaScript',
        debugLogging: JAVASCRIPT_DEBUG_LOGGING,
        docsUrl: JAVASCRIPT_DOCS_URL,
        group: 'popular',
        icon: 'shield',
        id: 'javascript-angular',
        isBrowser: true,
        keywords: ['angular', 'typescript', 'ts', 'javascript', 'js'],
        label: 'Angular',
        steps: (context) => [
            npmInstall('@exceptionless/browser'),
            {
                blocks: [
                    {
                        code: `import { ErrorHandler, Injectable } from "@angular/core";
import { bootstrapApplication } from "@angular/platform-browser";
import { Exceptionless, toError } from "@exceptionless/browser";
import { AppComponent } from "./app/app.component";

${javascriptStartup(context)}

@Injectable()
export class ExceptionlessErrorHandler implements ErrorHandler {
  handleError(error: unknown): void {
    console.error(error);
    Exceptionless.submitException(toError(error));
  }
}

bootstrapApplication(AppComponent, {
  providers: [{ provide: ErrorHandler, useClass: ExceptionlessErrorHandler }]
});`,
                        language: 'javascript'
                    }
                ],
                description: 'Start the client in main.ts and route Angular’s errors to Exceptionless with an ErrorHandler.',
                title: 'Start Exceptionless and add an ErrorHandler'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup, for example from a button click.',
                title: 'Send a test error'
            }
        ],
        tip: 'Angular catches errors itself, so the ErrorHandler is what passes them on to me.'
    },
    {
        category: 'Mobile',
        debugLogging: JAVASCRIPT_DEBUG_LOGGING,
        docsUrl: REACT_NATIVE_DOCS_URL,
        group: 'popular',
        icon: 'smartphone',
        id: 'javascript-react-native',
        keywords: ['react native', 'react-native', 'ios', 'android', 'mobile', 'javascript', 'typescript'],
        label: 'React Native',
        steps: (context) => [
            {
                blocks: [
                    {
                        code: 'npm install @exceptionless/react-native @react-native-async-storage/async-storage\ncd ios && pod install',
                        language: 'shellscript'
                    }
                ],
                description: 'AsyncStorage keeps queued events across restarts, so install it alongside the client.',
                title: 'Install the client'
            },
            reactNativeStartupStep(context),
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup, for example from a button press.',
                title: 'Send a test error'
            }
        ],
        tip: 'JavaScript errors are captured right away; native iOS crashes are reported on the next launch.'
    },
    {
        category: 'Mobile',
        debugLogging: JAVASCRIPT_DEBUG_LOGGING,
        docsUrl: REACT_NATIVE_DOCS_URL,
        group: 'popular',
        icon: 'tablet-smartphone',
        id: 'javascript-expo',
        keywords: ['expo', 'react native', 'ios', 'android', 'mobile', 'javascript', 'typescript'],
        label: 'Expo',
        steps: (context) => [
            {
                blocks: [{ code: 'npx expo install @exceptionless/react-native @react-native-async-storage/async-storage', language: 'shellscript' }],
                description: 'AsyncStorage keeps queued events across restarts, so install it alongside the client.',
                title: 'Install the client'
            },
            {
                blocks: [
                    {
                        code: `{
  "expo": {
    "plugins": ["@exceptionless/react-native/expo-plugin"]
  }
}`,
                        language: 'json'
                    }
                ],
                description: 'Add the config plugin to app.json for development and standalone builds. Expo Go still reports JavaScript errors.',
                title: 'Add the Expo config plugin'
            },
            reactNativeStartupStep(context),
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup, for example from a button press.',
                title: 'Send a test error'
            }
        ],
        tip: 'Expo Go is fine for JavaScript errors; native crash reporting needs a development build.'
    },
    {
        category: '.NET',
        debugLogging: DOTNET_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'popular',
        icon: 'app-window',
        id: 'dotnet-wpf',
        keywords: ['wpf', 'windows', 'desktop', 'xaml', 'csharp', 'c#', 'dotnet'],
        label: 'WPF (.NET)',
        steps: (context) => desktopDotnetSteps('Exceptionless.Wpf', 'App.xaml.cs', context),
        tip: 'Register() also hooks the dispatcher, so UI-thread crashes are reported too.'
    },
    {
        category: '.NET',
        debugLogging: DOTNET_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'popular',
        icon: 'panels-top-left',
        id: 'dotnet-winforms',
        keywords: ['winforms', 'windows forms', 'windows', 'desktop', 'csharp', 'c#', 'dotnet'],
        label: 'Windows Forms (.NET)',
        steps: (context) => desktopDotnetSteps('Exceptionless.Windows', 'Program.cs', context),
        tip: 'Register() also hooks thread exceptions, so UI crashes are reported too.'
    },
    {
        aliases: ['bash'],
        category: 'Command line',
        debugLogging: RAW_HTTP_DEBUG_LOGGING,
        docsUrl: API_DOCS_URL,
        group: 'popular',
        icon: 'braces',
        id: 'curl',
        isRawHttp: true,
        keywords: ['curl', 'bash', 'shell', 'http', 'rest', 'api', 'go', 'golang', 'python', 'ruby', 'php', 'java', 'rust', 'any language', 'linux', 'macos'],
        label: 'REST API (curl)',
        steps: ({ apiKey, submissionServerUrl }) => [
            {
                blocks: [
                    {
                        code: `curl "${submissionServerUrl}/api/v2/events" \\
  --request POST \\
  --header "Authorization: Bearer ${apiKey}" \\
  --header "Content-Type: application/json" \\
  --data-binary '{"type":"error","environment":"production","@simple_error":{"message":"${TEST_ERROR_MESSAGE}","type":"System.Exception","stack_trace":"   at Setup.SendTestError()"}}'`,
                        language: 'shellscript'
                    }
                ],
                description: 'Run this in your terminal. Any language that can make an HTTP request can send events the same way.',
                title: 'Send a test error'
            }
        ],
        tip: 'No client needed: any language that can POST JSON can send events this way.'
    },
    {
        category: 'Command line',
        debugLogging: RAW_HTTP_DEBUG_LOGGING,
        docsUrl: API_DOCS_URL,
        group: 'popular',
        icon: 'square-chevron-right',
        id: 'powershell',
        isRawHttp: true,
        keywords: ['powershell', 'pwsh', 'windows', 'script', 'http', 'rest', 'api'],
        label: 'PowerShell',
        steps: ({ apiKey, submissionServerUrl }) => [
            {
                blocks: [
                    {
                        code: `$body = @{
  type = "error"
  environment = "production"
  "@simple_error" = @{
    message = "${TEST_ERROR_MESSAGE}"
    type = "System.Exception"
    stack_trace = "   at Setup.SendTestError()"
  }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post -Uri "${submissionServerUrl}/api/v2/events" \`
  -Headers @{ Authorization = "Bearer ${apiKey}" } \`
  -ContentType "application/json" -Body $body`,
                        language: 'powershell'
                    }
                ],
                description: 'Run this in PowerShell to send one error event.',
                title: 'Send a test error'
            }
        ],
        tip: 'Handy for scripts and scheduled tasks: wrap risky work in try/catch and post the error.'
    },
    {
        category: '.NET Framework',
        debugLogging: DOTNET_FRAMEWORK_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'legacy',
        icon: 'square-terminal',
        id: 'dotnet-legacy-console',
        keywords: ['console', 'service', 'framework', 'net framework', 'csharp', 'c#', 'dotnet', 'windows service'],
        label: 'Console app (.NET Framework)',
        steps: (context) => [
            legacyInstall('Exceptionless'),
            {
                blocks: [
                    {
                        code: `using Exceptionless;
${context.sdkServerUrl ? `\nExceptionlessClient.Default.Configuration.ServerUrl = "${context.sdkServerUrl}";` : ''}
ExceptionlessClient.Default.Startup("${context.apiKey}");

${DOTNET_TEST_ERROR}

// Send anything queued before the app exits.
ExceptionlessClient.Default.ProcessQueueAsync().GetAwaiter().GetResult();`,
                        language: 'csharp'
                    }
                ],
                description: 'Start the client in Main. The middle line sends a test error so you can see it arrive.',
                title: 'Start Exceptionless and send a test error'
            }
        ],
        tip: 'You can keep the API key in app.config instead and call Startup() with no arguments.'
    },
    {
        category: '.NET Framework',
        debugLogging: DOTNET_FRAMEWORK_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'legacy',
        icon: 'server',
        id: 'dotnet-legacy-mvc',
        keywords: ['asp.net', 'aspnet', 'mvc', 'framework', 'net framework', 'iis', 'csharp', 'c#'],
        label: 'ASP.NET MVC',
        steps: (context) => [
            legacyInstall('Exceptionless.Mvc'),
            legacyConfigStep('web.config', context),
            legacyModuleStep('Exceptionless.Mvc.ExceptionlessModule, Exceptionless.Mvc'),
            dotnetTestError('Run this once from a controller action, for example a temporary test endpoint.')
        ],
        tip: 'The HTTP module starts the client for you, so no startup code is needed.'
    },
    {
        category: '.NET Framework',
        debugLogging: DOTNET_FRAMEWORK_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'legacy',
        icon: 'server',
        id: 'dotnet-legacy-webapi',
        keywords: ['asp.net', 'aspnet', 'web api', 'webapi', 'owin', 'framework', 'net framework', 'iis', 'csharp', 'c#'],
        label: 'ASP.NET Web API',
        steps: (context) => [
            legacyInstall('Exceptionless.WebApi'),
            legacyConfigStep('web.config', context),
            {
                blocks: [
                    {
                        code: `using Exceptionless;

public static class WebApiConfig {
    public static void Register(HttpConfiguration config) {
        ExceptionlessClient.Default.RegisterWebApi(config);
        // ...your routes
    }
}`,
                        language: 'csharp'
                    }
                ],
                description: 'Call RegisterWebApi with your HttpConfiguration. Hosted in ASP.NET, pass GlobalConfiguration.Configuration instead.',
                title: 'Register Exceptionless with Web API'
            },
            dotnetTestError('Run this once from a controller action, for example a temporary test endpoint.')
        ],
        tip: 'RegisterWebApi starts the client and adds the exception logger and filters.'
    },
    {
        category: '.NET Framework',
        debugLogging: DOTNET_FRAMEWORK_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'legacy',
        icon: 'server',
        id: 'dotnet-legacy-webforms',
        keywords: ['asp.net', 'aspnet', 'web forms', 'webforms', 'framework', 'net framework', 'iis', 'csharp', 'c#'],
        label: 'ASP.NET Web Forms',
        steps: (context) => [
            legacyInstall('Exceptionless.Web'),
            legacyConfigStep('web.config', context),
            legacyModuleStep('Exceptionless.Web.ExceptionlessModule, Exceptionless.Web'),
            dotnetTestError('Run this once from a page, for example a temporary test button.')
        ],
        tip: 'The HTTP module starts the client for you, so no startup code is needed.'
    },
    {
        category: '.NET Framework',
        debugLogging: DOTNET_FRAMEWORK_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'legacy',
        icon: 'panels-top-left',
        id: 'dotnet-legacy-winforms',
        keywords: ['winforms', 'windows forms', 'windows', 'desktop', 'framework', 'net framework', 'csharp', 'c#'],
        label: 'Windows Forms (.NET Framework)',
        steps: (context) => [
            legacyInstall('Exceptionless.Windows'),
            legacyConfigStep('app.config', context),
            {
                blocks: [
                    {
                        code: `using Exceptionless;

internal static class Program {
    [STAThread]
    private static void Main() {
        ExceptionlessClient.Default.Register();

        Application.Run(new MainForm());
    }
}`,
                        language: 'csharp'
                    }
                ],
                description: 'Register Exceptionless in Program.cs before Application.Run.',
                title: 'Register Exceptionless in Program.cs'
            },
            dotnetTestError('Add this after Register() and run your app once.')
        ],
        tip: 'Register() also hooks thread exceptions, so UI crashes are reported too.'
    },
    {
        category: '.NET Framework',
        debugLogging: DOTNET_FRAMEWORK_DEBUG_LOGGING,
        docsUrl: DOTNET_DOCS_URL,
        group: 'legacy',
        icon: 'app-window',
        id: 'dotnet-legacy-wpf',
        keywords: ['wpf', 'windows', 'desktop', 'xaml', 'framework', 'net framework', 'csharp', 'c#'],
        label: 'WPF (.NET Framework)',
        steps: (context) => [
            legacyInstall('Exceptionless.Wpf'),
            legacyConfigStep('app.config', context),
            {
                blocks: [
                    {
                        code: `using Exceptionless;

public partial class App : Application {
    private void Application_Startup(object sender, StartupEventArgs e) {
        ExceptionlessClient.Default.Register();
    }
}`,
                        language: 'csharp'
                    }
                ],
                description: 'Register Exceptionless when your app starts. Point Startup="Application_Startup" at this handler in App.xaml.',
                title: 'Register Exceptionless in App.xaml.cs'
            },
            dotnetTestError('Add this after Register() and run your app once.')
        ],
        tip: 'Register() also hooks the dispatcher, so UI-thread crashes are reported too.'
    }
];

function reactNativeStartupStep(context: PlatformSetupContext): PlatformStep {
    return {
        blocks: [
            {
                code: `import { useEffect } from "react";
import { Exceptionless, ExceptionlessErrorBoundary } from "@exceptionless/react-native";

export default function App() {
  useEffect(() => {
${javascriptStartup(context, '    ')}
  }, []);

  return (
    <ExceptionlessErrorBoundary>
      <Root />
    </ExceptionlessErrorBoundary>
  );
}`,
                language: 'javascript'
            }
        ],
        description: 'Start the client once when your app mounts and wrap your app in the error boundary.',
        title: 'Start Exceptionless'
    };
}

export const PLATFORMS: readonly Platform[] = platforms;
export const POPULAR_PLATFORMS: readonly Platform[] = platforms.filter((platform) => platform.group === 'popular');
export const LEGACY_PLATFORMS: readonly Platform[] = platforms.filter((platform) => platform.group === 'legacy');

/** Finds the platform for a canonical id or a previously used `?type=` value. */
export function findPlatform(type: null | string | undefined): Platform | undefined {
    if (!type) {
        return undefined;
    }

    const value = type.trim().toLowerCase();
    return platforms.find((platform) => platform.id === value || platform.aliases?.includes(value));
}

/** Matches the label, category, and keywords. Every word of the query must match. */
export function searchPlatforms(query: string, candidates: readonly Platform[] = platforms): Platform[] {
    const terms = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
    if (terms.length === 0) {
        return [...candidates];
    }

    return candidates.filter((platform) => {
        const haystack = [platform.label, platform.category, platform.id, ...platform.keywords].join(' ').toLowerCase();
        return terms.every((term) => haystack.includes(term));
    });
}
