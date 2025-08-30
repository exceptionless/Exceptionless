<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A, CodeBlock, H3, Muted, P } from '$comp/typography';
    import * as Select from '$comp/ui/select';
    import { Separator } from '$comp/ui/separator';
    import { env } from '$env/dynamic/public';
    import { getProjectDefaultTokenQuery, patchToken } from '$features/tokens/api.svelte';
    import EnableTokenDialog from '$features/tokens/components/dialogs/enable-token-dialog.svelte';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener } from 'runed';
    import { toast } from 'svelte-sonner';

    // Project ID from route params
    const projectId = page.params.projectId || '';

    const defaultTokenQuery = getProjectDefaultTokenQuery({
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    const apiKey = $derived(defaultTokenQuery.data?.id || 'YOUR_API_KEY');
    const serverUrl = env.PUBLIC_API_URL || window.location.origin;
    const isTokenDisabled = $derived(defaultTokenQuery.data?.is_disabled ?? false);
    const isTokenSuspended = $derived(defaultTokenQuery.data?.is_suspended ?? false);

    let toastId = $state<number | string>();
    let openEnableTokenDialog = $state(false);

    const enableTokenMutation = patchToken({
        route: {
            get id() {
                return defaultTokenQuery.data?.id || '';
            }
        }
    });

    async function enableToken() {
        toast.dismiss(toastId);

        try {
            await enableTokenMutation.mutateAsync({ is_disabled: false });
            toastId = toast.success(`Successfully enabled API key`);
        } catch (error) {
            toastId = toast.error('Failed to enable API key. Please try again.');
            throw error;
        }
    }

    interface ProjectType {
        config?: string;
        id: string;
        label: string;
        package?: string;
        platform: string;
    }

    const projectTypes: ProjectType[] = [
        { id: 'bash', label: 'Bash Shell', platform: 'Command Line' },
        { id: 'powershell', label: 'PowerShell', platform: 'Command Line' },

        { id: 'dotnet-console', label: 'Console and Service applications', package: 'Exceptionless', platform: '.NET' },
        { id: 'dotnet-aspnetcore', label: 'ASP.NET Core', package: 'Exceptionless.AspNetCore', platform: '.NET' },
        { config: 'app.config', id: 'dotnet-wpf', label: 'Windows Presentation Foundation (WPF)', package: 'Exceptionless.Wpf', platform: '.NET' },
        { config: 'app.config', id: 'dotnet-winforms', label: 'Windows Forms', package: 'Exceptionless.Windows', platform: '.NET' },

        { id: 'javascript-browser', label: 'Browser applications', package: 'Exceptionless.JavaScript', platform: 'JavaScript' },
        { id: 'javascript-nodejs', label: 'Node.js', package: 'Exceptionless.Node', platform: 'JavaScript' },

        { id: 'dotnet-legacy-console', label: 'Console and Service applications', package: 'Exceptionless', platform: '.NET Legacy' },
        { config: 'web.config', id: 'dotnet-legacy-mvc', label: 'ASP.NET MVC', package: 'Exceptionless.Mvc', platform: '.NET Legacy' },
        { config: 'web.config', id: 'dotnet-legacy-webapi', label: 'ASP.NET Web API', package: 'Exceptionless.WebApi', platform: '.NET Legacy' },
        { config: 'web.config', id: 'dotnet-legacy-webforms', label: 'ASP.NET Web Forms', package: 'Exceptionless.Web', platform: '.NET Legacy' },
        { config: 'app.config', id: 'dotnet-legacy-winforms', label: 'Windows Forms', package: 'Exceptionless.Windows', platform: '.NET Legacy' },
        { config: 'app.config', id: 'dotnet-legacy-wpf', label: 'Windows Presentation Foundation (WPF)', package: 'Exceptionless.Wpf', platform: '.NET Legacy' }
    ];

    const projectTypesGroupedByPlatform = Object.groupBy(projectTypes, (p) => p.platform);

    const queryParams = queryParamsState({
        default: {
            redirect: false,
            type: undefined
        },
        pushHistory: true,
        schema: {
            redirect: 'boolean',
            type: 'string'
        }
    });

    let selectedProjectType = $state<null | ProjectType>(null);

    $effect(() => {
        // Handle case where pop state loses the limit
        if (queryParams.type) {
            const found = projectTypes.find((p) => p.id === queryParams.type);
            if (found) {
                selectedProjectType = found;
            } else {
                queryParams.type = null;
                selectedProjectType = null;
            }
        }
    });

    const isCommandLine = $derived(selectedProjectType?.platform === 'Command Line');
    const isDotNet = $derived(selectedProjectType?.platform === '.NET');
    const isDotNetLegacy = $derived(selectedProjectType?.platform === '.NET Legacy');
    const isJavaScript = $derived(selectedProjectType?.platform === 'JavaScript');
    const isNode = $derived(selectedProjectType?.package === 'Exceptionless.Node');
    const isBashShell = $derived(selectedProjectType?.id === 'bash');
    const clientDocumentationUrl = $derived.by(() => {
        if (isDotNet || isDotNetLegacy) {
            return 'https://exceptionless.com/docs/clients/dotnet/';
        }

        if (isJavaScript) {
            return 'https://exceptionless.com/docs/clients/javascript/';
        }

        return 'https://exceptionless.com/docs/api/';
    });

    const codeSamples = $derived({
        aspNetCore: `using Exceptionless;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddExceptionless("${apiKey}");

var app = builder.Build();
app.UseExceptionless();`,

        bashShell: `curl "${serverUrl}/api/v2/events" \\
    --request POST \\
    --header "Authorization: Bearer ${apiKey}" \\
    --header "Content-Type: application/json" \\
    --data-binary "[{'type':'log','message':'Hello World!'}]"`,

        browserJs: `import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.apiKey = "${apiKey}";
});`,

        exceptionless: `using Exceptionless;

ExceptionlessClient.Default.Startup("${apiKey}");`,

        legacyAppConfigSectionXml: `<exceptionless apiKey="${apiKey}" />`,

        nodeJs: `import { Exceptionless } from "@exceptionless/node";

await Exceptionless.startup(c => {
  c.apiKey = "${apiKey}";
});`,

        powerShell: `$body = @{
 "type"="log"
 "message"="Hello World!"
} | ConvertTo-Json

$header = @{
 "Authorization"="Bearer ${apiKey}"
 "Content-Type"="application/json"
}

Invoke-RestMethod -Uri "${serverUrl}/api/v2/events" -Method "Post" -Body $body -Headers $header`,

        webApi: `public static void Register(HttpConfiguration config) {
  config.AddExceptionless("${apiKey}");
}`,

        webApiInAspNet: `protected void Application_Start() {
  ExceptionlessClient.Default.Configuration.ApiKey = "${apiKey}";
  ExceptionlessClient.Default.Startup();
}`,
        webApiRegister: `using Exceptionless;

public class Startup {
  public void Configuration(IAppBuilder app) {
    var config = new HttpConfiguration();
    config.Routes.MapHttpRoute(name: "DefaultApi", routeTemplate: "api/{controller}/{id}", defaults: new {
        id = RouteParameter.Optional
    });
    app.UseWebApi(config);

    ExceptionlessClient.Default.RegisterWebApi(config);
  }
}`,

        webApiRegisterAspNet: `Exceptionless.ExceptionlessClient.Default.RegisterWebApi(GlobalConfiguration.Configuration)`,
        windowsAttributeConfiguration: `[assembly: Exceptionless.Configuration.Exceptionless("${apiKey}")]`,
        windowsRegister: `using Exceptionless;

internal static class Program {
  [STAThread]
  private static void Main() {
    ExceptionlessClient.Default.Register();

    Application.Run(new MainForm());
  }
}`,
        wpfRegister: `using Exceptionless;

public partial class App : Application {
  private void Application_Startup(object sender, StartupEventArgs e) {
    ExceptionlessClient.Default.Register();
  }
}`
    });

    useEventListener(document, 'PersistentEventChanged', async () => {
        if (queryParams.redirect) {
            await goto('/next/issues');
        }
    });

    function openChat() {
        // TODO: Implement chat opening logic
    }
</script>

<div class="space-y-6">
    <div>
        <H3>Download & Configure Project</H3>
        <Muted>The Exceptionless client can be integrated into your project in just a few easy steps.</Muted>
    </div>

    {#if isTokenDisabled}
        <Notification variant="destructive">
            <NotificationTitle>API Key Disabled</NotificationTitle>
            <NotificationDescription>
                <P
                    >The configuration steps won't work until your project's API key is enabled. Please <A onclick={() => (openEnableTokenDialog = true)}
                        >enable your API key</A
                    > to continue.</P
                >
            </NotificationDescription>
        </Notification>
    {:else if isTokenSuspended}
        <Notification variant="destructive">
            <NotificationTitle>API Key Disabled</NotificationTitle>
            <NotificationDescription>
                <P
                    >The configuration steps won't work while your project's API key is suspended. Please <A onclick={openChat}>contact support</A> for more information.</P
                >
            </NotificationDescription>
        </Notification>
    {/if}

    <Separator />

    <ol class="my-6 ml-6 list-decimal [&>li]:mt-2">
        <li>
            <P>Select your project type.</P>
            <Select.Root
                type="single"
                bind:value={queryParams.type as string | undefined}
                onValueChange={(value) => {
                    selectedProjectType = projectTypes.find((P) => P.id === value) || null;
                    queryParams.type = value;
                }}
            >
                <Select.Trigger class="w-full">
                    <span
                        >{#if selectedProjectType}
                            {selectedProjectType.platform}: {selectedProjectType.label}
                        {:else}
                            Please select a project type
                        {/if}</span
                    >
                </Select.Trigger>
                <Select.Content>
                    {#each Object.entries(projectTypesGroupedByPlatform) as [platform, types = []], index (platform)}
                        <Select.Group>
                            <Select.Label class="text-primary">{platform}</Select.Label>
                            {#each types as type (type.id)}
                                <Select.Item value={type.id}>
                                    {type.label}
                                </Select.Item>
                            {/each}
                        </Select.Group>
                        {#if index < Object.keys(projectTypesGroupedByPlatform).length - 1}
                            <Select.Separator />
                        {/if}
                    {/each}
                </Select.Content>
            </Select.Root>
        </li>

        {#if selectedProjectType}
            {#if isCommandLine}
                <li>
                    <P>Execute the following in your shell.</P>
                    <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                        {#if isBashShell}
                            <CodeBlock code={codeSamples.bashShell} language="shellscript" />
                        {:else}
                            <CodeBlock code={codeSamples.powerShell} language="powershell" />
                        {/if}
                        <div class="absolute top-2 right-2">
                            <CopyToClipboardButton value={isBashShell ? codeSamples.bashShell : codeSamples.powerShell} />
                        </div>
                    </div>
                </li>
            {:else if isDotNet}
                <li>
                    <P
                        >Install the <A href="https://www.nuget.org/packages/{selectedProjectType.package}/" target="_blank"
                            ><strong>{selectedProjectType.package}</strong> NuGet package</A
                        > in your .NET project by executing this command from the project directory.</P
                    >
                    <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                        <CodeBlock code="dotnet add package {selectedProjectType.package}" language="shellscript" />
                        <div class="absolute top-2 right-2">
                            <CopyToClipboardButton value={`dotnet add package ${selectedProjectType.package}`} />
                        </div>
                    </div>
                </li>
                {#if selectedProjectType.package === 'Exceptionless'}
                    <li>
                        <P
                            >On app startup, import the Exceptionless namespace and call the client.Startup() extension method to wire up to any runtime
                            specific error handlers and read any available configuration.</P
                        >
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.exceptionless} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.exceptionless} />
                            </div>
                        </div>
                        <P
                            >This library is platform-agnostic and is compiled against different runtimes. Depending on the referenced runtime, Exceptionless
                            will attempt to wire up to available error handlers and attempt to discover configuration settings available to that runtime. For
                            these reasons if you are on a known platform then use the platform specific package to save you time configuring while giving you
                            more contextual information. For more information and configuration examples please read the <A
                                href="https://exceptionless.com/docs/clients/dotnet/configuration/"
                                target="_blank">Exceptionless Configuration documentation</A
                            > for more information.</P
                        >
                    </li>
                {:else if selectedProjectType.package === 'Exceptionless.AspNetCore'}
                    <li>
                        <P>You must import the Exceptionless namespace and add the following code to register and configure the Exceptionless client.</P>
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.aspNetCore} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.aspNetCore} />
                            </div>
                        </div>
                        <P
                            >In order to start gathering unhandled exceptions, you need to register the Exceptionless middleware after building your application
                            as shown above. Alternatively, you can use different overloads of the AddExceptionless method for additional configuration options.</P
                        >
                    </li>
                {:else if selectedProjectType.package === 'Exceptionless.Windows' || selectedProjectType.package === 'Exceptionless.Wpf'}
                    <li>
                        <P>Configure your Exceptionless assembly attribute to your projects AssemblyInfo.cs file.</P>
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.windowsAttributeConfiguration} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.windowsAttributeConfiguration} />
                            </div>
                        </div>
                    </li>
                    {#if selectedProjectType.package === 'Exceptionless.Wpf'}
                        <li>
                            <P
                                >Finally, import the Exceptionless namespace and include the following line of code in your App.xaml.cs file to enable reporting
                                of unhandled exceptions.</P
                            >
                            <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                                <CodeBlock code={codeSamples.wpfRegister} language="csharp" />
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value={codeSamples.wpfRegister} />
                                </div>
                            </div>
                        </li>
                    {:else}
                        <li>
                            <P
                                >Finally, import the Exceptionless namespace and include the following line of code in your Program.cs file to enable reporting
                                of unhandled exceptions.</P
                            >
                            <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                                <CodeBlock code={codeSamples.windowsRegister} language="csharp" />
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value={codeSamples.windowsRegister} />
                                </div>
                            </div>
                        </li>
                    {/if}
                {/if}
            {:else if isDotNetLegacy}
                <li>
                    <P
                        >Install the <A href="https://www.nuget.org/packages/{selectedProjectType.package}/" target="_blank"
                            ><strong>{selectedProjectType.package}</strong> NuGet package</A
                        > to your Visual Studio project by running this command in the <A
                            href="http://docs.nuget.org/docs/start-here/using-the-package-manager-console"
                            target="_blank">Package Manager Console.</A
                        ></P
                    >
                    <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                        <CodeBlock code="Install-Package {selectedProjectType.package}" language="shellscript" />
                        <div class="absolute top-2 right-2">
                            <CopyToClipboardButton value={`Install-Package ${selectedProjectType.package}`} />
                        </div>
                    </div>
                </li>
                {#if selectedProjectType.package === 'Exceptionless'}
                    <li>
                        <P
                            >On app startup, import the Exceptionless namespace and call the client.Startup() extension method to wire up to any runtime
                            specific error handlers and read any available configuration.</P
                        >
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.exceptionless} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.exceptionless} />
                            </div>
                        </div>
                        <P
                            >For more information and additional configuration methods please read the <A
                                href="https://exceptionless.com/docs/clients/dotnet/configuration/"
                                target="_blank">Exceptionless Configuration documentation</A
                            > for more information.</P
                        >
                    </li>
                {:else if selectedProjectType.package === 'Exceptionless.Windows' || selectedProjectType.package === 'Exceptionless.Wpf'}
                    <li>
                        <P
                            >Configure your Exceptionless API key in your project's app.config file, and add it under the Exceptionless section within the file.</P
                        >
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.legacyAppConfigSectionXml} language="xml" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.legacyAppConfigSectionXml} />
                            </div>
                        </div>
                    </li>
                    {#if selectedProjectType.package === 'Exceptionless.Wpf'}
                        <li>
                            <P
                                >Finally, import the Exceptionless namespace and include the following line of code in your App.xaml.cs file to enable reporting
                                of unhandled exceptions.</P
                            >
                            <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                                <CodeBlock code={codeSamples.wpfRegister} language="csharp" />
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value={codeSamples.wpfRegister} />
                                </div>
                            </div>
                        </li>
                    {:else}
                        <li>
                            <P
                                >Finally, import the Exceptionless namespace and include the following line of code in your Program.cs file to enable reporting
                                of unhandled exceptions.</P
                            >
                            <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                                <CodeBlock code={codeSamples.windowsRegister} language="csharp" />
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value={codeSamples.windowsRegister} />
                                </div>
                            </div>
                        </li>
                    {/if}
                {/if}

                {#if selectedProjectType.package === 'Exceptionless.Mvc' || selectedProjectType.package === 'Exceptionless.Web' || selectedProjectType.package === 'Exceptionless.WebApi'}
                    <li>
                        <P
                            >Configure your Exceptionless API key in your project's web.config file, and add it under the Exceptionless section within the file.</P
                        >
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.legacyAppConfigSectionXml} language="xml" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.legacyAppConfigSectionXml} />
                            </div>
                        </div>
                    </li>
                {/if}

                {#if selectedProjectType.package === 'Exceptionless.WebApi'}
                    <li>
                        <P
                            >Finally, you must import the Exceptionless namespace and call <CodeBlock
                                code="ExceptionlessClient.Default.RegisterWebApi(config)"
                                language="csharp"
                                class="inline-block"
                            /> method with an instance of HttpConfiguration during the startup of your app.</P
                        >
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.webApiRegister} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.webApiRegister} />
                            </div>
                        </div>
                        <P>If you are hosting Web API inside of ASP.NET, you would register Exceptionless using GlobalConfiguration.</P>
                        <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                            <CodeBlock code={codeSamples.webApiRegisterAspNet} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.webApiRegisterAspNet} />
                            </div>
                        </div>
                    </li>
                {/if}
            {/if}

            {#if isJavaScript}
                <li>
                    <P
                        >Install the <strong>{selectedProjectType.package}</strong> npm package in your JavaScript project by running this command in the project
                        directory.</P
                    >
                    <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                        {#if isNode}
                            <CodeBlock code="npm install @exceptionless/node --save" language="shellscript" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value="npm install @exceptionless/node --save" />
                            </div>
                        {:else}
                            <CodeBlock code="npm install @exceptionless/browser --save" language="shellscript" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value="npm install @exceptionless/browser --save" />
                            </div>
                        {/if}
                    </div>
                </li>
                <li>
                    <P>Configure the ExceptionlessClient with your Exceptionless API key.</P>
                    <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                        {#if !isNode}
                            <CodeBlock code={codeSamples.browserJs} language="javascript" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.browserJs} />
                            </div>
                        {:else}
                            <CodeBlock code={codeSamples.nodeJs} language="javascript" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.nodeJs} />
                            </div>
                        {/if}
                    </div>
                </li>
            {/if}
        {/if}
    </ol>

    {#if selectedProjectType}
        <P
            ><strong>That's it!</strong>
            {#if isCommandLine}
                You can now send data to Exceptionless using the command line.
            {:else}
                Your project should now automatically be sending all unhandled exceptions to Exceptionless!
                {#if isDotNet}
                    You can also <A href="https://exceptionless.com/docs/clients/dotnet/sending-events/" target="_blank"
                        >send handled exceptions to Exceptionless</A
                    > using
                    <CodeBlock code="ex.ToExceptionless().Submit();" language="csharp" class="inline-block" />
                {:else if isJavaScript}
                    You can also <A href="https://exceptionless.com/docs/clients/javascript/sending-events/" target="_blank"
                        >send handled exceptions to Exceptionless</A
                    > using
                    <CodeBlock code="await Exceptionless.submitException(ex);" language="javascript" class="inline-block" />
                {/if}
            {/if}
        </P>

        <Notification>
            <NotificationTitle>Need more help?</NotificationTitle>
            <NotificationDescription>
                <P>
                    For more information and troubleshooting tips, view the
                    <A href={clientDocumentationUrl} target="_blank">Exceptionless documentation</A>.
                    {#if isJavaScript}
                        If you're using a specific framework (like Angular, Express.js, React, Svelte or Vue), be sure to check out our
                        <A href="https://exceptionless.com/docs/clients/javascript/guides/" target="_blank">JavaScript Framework Guides</A> for optimized integration
                        steps.
                    {/if}
                </P>
            </NotificationDescription>
        </Notification>
    {/if}

    {#if isTokenDisabled}
        <EnableTokenDialog open={openEnableTokenDialog} id={defaultTokenQuery.data?.id || ''} notes={defaultTokenQuery.data?.notes} enable={enableToken} />
    {/if}
</div>
