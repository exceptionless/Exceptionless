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
const REACT_NATIVE_DOCS_URL = 'https://exceptionless.com/docs/clients/javascript/guides/react-native/';
const API_DOCS_URL = 'https://exceptionless.com/docs/api/';

const DOTNET_DEBUG_LOGGING =
    'Add c.UseFileLogger("exceptionless.log", Exceptionless.Logging.LogLevel.Debug) to your AddExceptionless configuration, then check exceptionless.log. "Sent 1 events" means the server accepted it. builder.AddExceptionless(c => c.UseFileLogger(...)) still reads appsettings.json.';
const DOTNET_CLIENT_DEBUG_LOGGING =
    'Call ExceptionlessClient.Default.Configuration.UseFileLogger("exceptionless.log", Exceptionless.Logging.LogLevel.Debug) before Startup() or Register(), then check exceptionless.log. "Sent 1 events" means the server accepted it.';
const DOTNET_WEB_FRAMEWORK_DEBUG_LOGGING =
    'A relative log path resolves against the IIS working directory, which the app pool usually cannot write to. Use an absolute path in a folder it can write to: call ExceptionlessClient.Default.Configuration.UseFileLogger(Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "exceptionless.log"), Exceptionless.Logging.LogLevel.Debug) in Application_Start (or set enableLogging="true" and an absolute logPath on the <exceptionless> element), then check the log for submission errors.';
const DOTNET_FRAMEWORK_DEBUG_LOGGING =
    'Add enableLogging="true" logPath="exceptionless.log" to the <exceptionless> element (or call ExceptionlessClient.Default.Configuration.UseFileLogger("exceptionless.log")), then check the log for submission errors.';
const JAVASCRIPT_DEBUG_LOGGING =
    'Call c.useDebugLogger() inside Exceptionless.startup to log what the client is doing to the console. "Sent 1 events" means the server accepted it.';
const RAW_HTTP_DEBUG_LOGGING = 'A 202 Accepted response means the event was queued. A 401 means the API key is wrong or disabled.';

// Thrown so the event has a stack trace.
const DOTNET_TEST_ERROR = `try { throw new Exception("${TEST_ERROR_MESSAGE}"); } catch (Exception ex) { ex.ToExceptionless().Submit(); }`;
const JAVASCRIPT_TEST_ERROR = `Exceptionless.submitException(new Error("${TEST_ERROR_MESSAGE}"));`;

function desktopDotnetSteps(packageName: string, entryPoint: 'App.xaml.cs' | 'Program.cs', context: PlatformSetupContext): PlatformStep[] {
    const startup =
        entryPoint === 'App.xaml.cs'
            ? `using System.Windows;
using Exceptionless;

namespace MyApp {
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
${dotnetDefaultClientConfiguration(context, '            ')}
            ExceptionlessClient.Default.Register();
            base.OnStartup(e);
        }
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
                    ? 'Register Exceptionless in OnStartup, which runs before the StartupUri window opens, so App.xaml needs no changes. Change `MyApp` to the namespace in x:Class in App.xaml.'
                    : 'Register Exceptionless before Application.Run so unhandled exceptions are reported.',
            title: `Register Exceptionless in ${entryPoint}`
        },
        dotnetTestError('Add this after Register() and run your app once.')
    ];
}

/** The hosting packages read an "Exceptionless" configuration section when AddExceptionless() has no arguments. */
function dotnetAppSettingsStep({ apiKey, sdkServerUrl }: PlatformSetupContext): PlatformStep {
    const settings = sdkServerUrl ? `"ApiKey": "${apiKey}",\n    "ServerUrl": "${sdkServerUrl}"` : `"ApiKey": "${apiKey}"`;
    return {
        blocks: [{ code: `{\n  "Exceptionless": {\n    ${settings}\n  }\n}`, language: 'json' }],
        description:
            'To keep settings out of code, put them in appsettings.json (or Exceptionless__ApiKey environment variables) and call builder.AddExceptionless() with no arguments.',
        optional: true,
        title: 'Keep settings in configuration'
    };
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
app.UseExceptionless();

app.Run();`,
                        language: 'csharp'
                    }
                ],
                description: 'Register the client and middleware in Program.cs. Keep your existing routes and middleware; app.Run() stays last.',
                title: 'Add Exceptionless to Program.cs'
            },
            dotnetTestError('Add this after app.UseExceptionless() and before app.Run(), run your app once, then remove it.'),
            dotnetAppSettingsStep(context),
            {
                blocks: [
                    { code: 'dotnet add package Exceptionless.Extensions.Logging', language: 'shellscript' },
                    { code: 'builder.Logging.AddExceptionless();', language: 'csharp' }
                ],
                description:
                    'Send ILogger logs to Exceptionless too. The provider sends whatever levels the Logging configuration of your app allows (Information and above by default), so adjust the levels under Logging in appsettings.json.',
                optional: true,
                title: 'Send your logs'
            }
        ],
        tip: 'Once the middleware is in, unhandled exceptions and 404s show up on their own.'
    },
    {
        category: '.NET',
        debugLogging: DOTNET_CLIENT_DEBUG_LOGGING,
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
                description:
                    'Start the client when your app starts. The Submit() line sends a test error so you can see it arrive; remove only that line afterwards.',
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
                        code: `try { throw new Exception("${TEST_ERROR_MESSAGE}"); }
catch (Exception ex) { host.Services.GetRequiredService<ExceptionlessClient>().SubmitException(ex); }`,
                        language: 'csharp'
                    }
                ],
                description: 'Add this just before host.Run(), run your worker once, then remove it.',
                title: 'Send a test error'
            },
            dotnetAppSettingsStep(context),
            {
                blocks: [
                    { code: 'dotnet add package Exceptionless.Extensions.Logging', language: 'shellscript' },
                    { code: 'builder.Logging.AddExceptionless();', language: 'csharp' }
                ],
                description:
                    "The host logs a worker's exceptions instead of crashing, so send ILogger logs to Exceptionless too. Which levels are sent follows the Logging configuration of your app (Information and above by default).",
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
                description: 'Add this in your entry file right after startup, load the page once, then remove it.',
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
                description:
                    'Run this once when your process starts, before the rest of your code. The client keeps unsent events in memory, so a hard crash can lose them; its synced settings go in a .exceptionless folder, so add it to .gitignore.',
                title: 'Start Exceptionless'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Run this once after startup. Queued events are sent before the process exits.',
                title: 'Send a test error'
            },
            {
                blocks: [
                    {
                        code: `app.use((error, req, res, next) => {
  Exceptionless.submitException(error);
  next(error);
});`,
                        language: 'javascript'
                    }
                ],
                description: 'Express catches route errors itself, so report them from an error handler added after your routes.',
                optional: true,
                title: 'Report Express route errors'
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

${javascriptStartup(context)}`,
                        language: 'javascript'
                    },
                    {
                        code: `<ExceptionlessErrorBoundary fallback={<p>Something went wrong.</p>}>
  <App />
</ExceptionlessErrorBoundary>`,
                        language: 'javascript'
                    }
                ],
                description:
                    'Start the client at the top of your entry file (main.jsx or main.tsx), then wrap <App /> in the error boundary where it renders, inside StrictMode if you use it.',
                title: 'Start Exceptionless and add the error boundary'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Add this in your entry file right after startup, load the page once, then remove it.',
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
import { Exceptionless, toError } from "@exceptionless/vue";
import App from "./App.vue";

${javascriptStartup(context)}

const app = createApp(App);
app.config.errorHandler = (err, instance, info) => {
  console.error(err);
  void Exceptionless.createException(toError(err)).setProperty("info", info).submit();
};
app.mount("#app");`,
                        language: 'javascript'
                    }
                ],
                description:
                    'Start the client in main.js or main.ts and hand component errors to Exceptionless. Keep your existing imports. Do not use the package ExceptionlessErrorHandler export: it attaches the whole component instance to the event, which can freeze the page.',
                title: 'Start Exceptionless and add the error handler'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Add this in your entry file right after startup, load the page once, then remove it.',
                title: 'Send a test error'
            }
        ],
        tip: 'The error handler reports component errors with the Vue info string attached.'
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
                        code: `// main.ts
import { bootstrapApplication } from "@angular/platform-browser";
import { Exceptionless } from "@exceptionless/browser";
import { App } from "./app/app";
import { appConfig } from "./app/app.config";

${javascriptStartup(context)}

bootstrapApplication(App, appConfig);`,
                        language: 'javascript'
                    },
                    {
                        code: `// app.config.ts
import { type ApplicationConfig, ErrorHandler, Injectable } from "@angular/core";
import { Exceptionless, toError } from "@exceptionless/browser";

@Injectable()
export class ExceptionlessErrorHandler implements ErrorHandler {
  handleError(error: unknown): void {
    console.error(error);
    Exceptionless.submitException(toError(error));
  }
}

export const appConfig: ApplicationConfig = {
  providers: [
    // ...your existing providers, without provideBrowserGlobalErrorListeners()
    { provide: ErrorHandler, useClass: ExceptionlessErrorHandler }
  ]
};`,
                        language: 'javascript'
                    }
                ],
                description:
                    'Start the client in main.ts, and add the ErrorHandler to your providers in app.config.ts. Remove provideBrowserGlobalErrorListeners() from the providers if it is there: it forwards window errors and unhandled rejections to ErrorHandler, which the browser client already reports, so each would arrive twice. Apps that bootstrap AppComponent work the same way.',
                title: 'Start Exceptionless and add an ErrorHandler'
            },
            {
                blocks: [{ code: JAVASCRIPT_TEST_ERROR, language: 'javascript' }],
                description: 'Add this in your entry file right after startup, load the page once, then remove it.',
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
                description: 'Add this at module scope right after startup. Run the app once, then remove it.',
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
                description: 'Add this at module scope right after startup. Run the app once, then remove it.',
                title: 'Send a test error'
            }
        ],
        tip: 'Expo Go is fine for JavaScript errors; native crash reporting needs a development build.'
    },
    {
        category: '.NET',
        debugLogging: DOTNET_CLIENT_DEBUG_LOGGING,
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
        debugLogging: DOTNET_CLIENT_DEBUG_LOGGING,
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
                        code: `using System;
using Exceptionless;

internal static class Program {
    static void Main() {${context.sdkServerUrl ? `\n        ExceptionlessClient.Default.Configuration.ServerUrl = "${context.sdkServerUrl}";` : ''}
        ExceptionlessClient.Default.Startup("${context.apiKey}");

        ${DOTNET_TEST_ERROR}

        // Send anything queued before the app exits.
        ExceptionlessClient.Default.ProcessQueueAsync().GetAwaiter().GetResult();
    }
}`,
                        language: 'csharp'
                    }
                ],
                description: 'Start the client in Main. The Submit() line sends a test error so you can see it arrive; remove only that line afterwards.',
                title: 'Start Exceptionless and send a test error'
            }
        ],
        tip: 'You can keep the API key in app.config instead and call Startup() with no arguments.'
    },
    {
        category: '.NET Framework',
        debugLogging: DOTNET_WEB_FRAMEWORK_DEBUG_LOGGING,
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
        debugLogging: DOTNET_WEB_FRAMEWORK_DEBUG_LOGGING,
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
        debugLogging: DOTNET_WEB_FRAMEWORK_DEBUG_LOGGING,
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
                        code: `using System.Windows;
using Exceptionless;

namespace MyApp {
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            ExceptionlessClient.Default.Register();
            base.OnStartup(e);
        }
    }
}`,
                        language: 'csharp'
                    }
                ],
                description:
                    'Register Exceptionless in OnStartup, which runs before the StartupUri window opens, so App.xaml needs no changes. Change `MyApp` to the namespace in x:Class in App.xaml.',
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
                code: `import { Exceptionless, ExceptionlessErrorBoundary } from "@exceptionless/react-native";

${javascriptStartup(context)}

export default function App() {
  return (
    <ExceptionlessErrorBoundary>
      <YourApp />
    </ExceptionlessErrorBoundary>
  );
}`,
                language: 'javascript'
            }
        ],
        description:
            'Start the client at module scope in your root file, outside any component, so it runs before the first render and the error boundary can report that render. Wrap your existing root component (YourApp here) in the error boundary.',
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
