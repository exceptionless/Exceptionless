<script lang="ts">
    import { page } from '$app/state';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { A, Code, CodeBlock, H3, Muted, P } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Select from '$comp/ui/select';
    import { Separator } from '$comp/ui/separator';
    import { env } from '$env/dynamic/public';
    import { getProjectTokensQuery } from '$features/tokens/api.svelte';
    import { UseClipboard } from '$lib/hooks/use-clipboard.svelte';
    import { toast } from 'svelte-sonner';

    // Project ID from route params
    const projectId = page.params.projectId || '';

    // Get project tokens
    const tokensQuery = getProjectTokensQuery({
        params: { limit: 1 },
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    const apiKey = $derived(tokensQuery.data?.data?.[0]?.id || 'YOUR_API_KEY');

    // Server URL for API requests
    const serverUrl = env.PUBLIC_API_URL || window.location.origin;

    // Define the project type interface
    interface ProjectType {
        config?: string;
        key: string;
        name: string;
        platform: string;
    }

    // Project type handling
    const projectTypes: ProjectType[] = [
        { key: 'Bash Shell', name: 'Bash Shell', platform: 'Command Line' },
        { key: 'PowerShell', name: 'PowerShell', platform: 'Command Line' },
        { key: 'Exceptionless', name: 'Console and Service applications', platform: '.NET' },
        { key: 'Exceptionless.AspNetCore', name: 'ASP.NET Core', platform: '.NET' },
        { config: 'web.config', key: 'Exceptionless.Mvc', name: 'ASP.NET MVC', platform: '.NET' },
        { config: 'web.config', key: 'Exceptionless.WebApi', name: 'ASP.NET Web API', platform: '.NET' },
        { config: 'web.config', key: 'Exceptionless.Web', name: 'ASP.NET Web Forms', platform: '.NET' },
        { config: 'app.config', key: 'Exceptionless.Windows', name: 'Windows Forms', platform: '.NET' },
        { config: 'app.config', key: 'Exceptionless.Wpf', name: 'Windows Presentation Foundation (WPF)', platform: '.NET' },
        { config: 'app.config', key: 'Exceptionless.Nancy', name: 'Nancy', platform: '.NET' },
        { key: 'Exceptionless.JavaScript', name: 'Browser applications', platform: 'JavaScript' },
        { key: 'Exceptionless.Node', name: 'Node.js', platform: 'JavaScript' }
    ];

    let selectedProjectType = $state<null | ProjectType>(null);

    // Helper functions to determine project type
    function isCommandLine(type: null | ProjectType): boolean {
        return type?.platform === 'Command Line';
    }

    function isDotNet(type: null | ProjectType): boolean {
        return type?.platform === '.NET';
    }

    function isJavaScript(type: null | ProjectType): boolean {
        return type?.platform === 'JavaScript';
    }

    function isNode(type: null | ProjectType): boolean {
        return type?.key === 'Exceptionless.Node';
    }

    function isBashShell(type: null | ProjectType): boolean {
        return type?.key === 'Bash Shell';
    }

    // Copy to clipboard handling
    const clipboard = new UseClipboard();

    async function copyApiKey() {
        await clipboard.copy(apiKey);
        if (clipboard.copied) {
            toast.success('Copied API key to clipboard');
        } else {
            toast.error('Failed to copy API key');
        }
    }

    // Code sample templates
    const codeSamples = $derived({
        aspNetCore: `using Exceptionless;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddExceptionless("${apiKey}");`,

        bashShell: `curl "${serverUrl}/api/v2/events" \\
    --request POST \\
    --header "Authorization: Bearer ${apiKey}" \\
    --header "Content-Type: application/json" \\
    --data-binary "[{'type':'log','message':'Hello World!'}]"`,

        browserJs: `import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.apiKey = "${apiKey}";
});`,

        exceptionlessDefault: `ExceptionlessClient.Default.ApiKey = "${apiKey}";
ExceptionlessClient.Default.Startup();`,

        nancy: `protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines) {
  base.ApplicationStartup(container, pipelines);
  pipelines.AddExceptionless("${apiKey}");
}`,

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

        unhandledException: `AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
  var exception = e.ExceptionObject as Exception;
  if (exception != null)
    exception.ToExceptionless().Submit();
};`,

        webApi: `public static void Register(HttpConfiguration config) {
  config.AddExceptionless("${apiKey}");
}`,

        webApiInAspNet: `protected void Application_Start() {
  ExceptionlessClient.Default.Configuration.ApiKey = "${apiKey}";
  ExceptionlessClient.Default.Startup();
}`,

        windows: `public App() {
  ExceptionlessClient.Default.Startup("${apiKey}");
  // Handle any WPF unhandled exceptions.
  Current.DispatcherUnhandledException += (sender, e) => {
    e.Exception.ToExceptionless().Submit();
    e.Handled = true;
  };
}`
    });
</script>

<div class="space-y-6">
    <div>
        <H3>Download & Configure Project</H3>
        <Muted>The Exceptionless client can be integrated into your project in just a few easy steps.</Muted>
    </div>
    <Separator />

    <ol class="list-decimal space-y-8 pl-5">
        <li>
            <P>Select your project type:</P>
            <Select.Root
                type="single"
                value={selectedProjectType?.key}
                onValueChange={(value) => {
                    const found = projectTypes.find((P) => P.key === value);
                    selectedProjectType = found || null;
                }}
            >
                <Select.Trigger class="w-full">
                    <span>{selectedProjectType?.name || 'Please select a project type'}</span>
                </Select.Trigger>
                <Select.Content>
                    <Select.Group>
                        <Select.GroupHeading>Command Line</Select.GroupHeading>
                        {#each projectTypes.filter((P) => P.platform === 'Command Line') as type (type.key)}
                            <Select.Item value={type.key}>
                                {type.name}
                            </Select.Item>
                        {/each}
                    </Select.Group>
                    <Select.Separator />
                    <Select.Group>
                        <Select.GroupHeading>.NET</Select.GroupHeading>
                        {#each projectTypes.filter((P) => P.platform === '.NET') as type (type.key)}
                            <Select.Item value={type.key}>
                                {type.name}
                            </Select.Item>
                        {/each}
                    </Select.Group>
                    <Select.Separator />
                    <Select.Group>
                        <Select.GroupHeading>JavaScript</Select.GroupHeading>
                        {#each projectTypes.filter((P) => P.platform === 'JavaScript') as type (type.key)}
                            <Select.Item value={type.key}>
                                {type.name}
                            </Select.Item>
                        {/each}
                    </Select.Group>
                </Select.Content>
            </Select.Root>
        </li>

        {#if selectedProjectType}
            {#if isCommandLine(selectedProjectType)}
                <li>
                    <P>Execute the following in your shell:</P>
                    <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                        {#if isBashShell(selectedProjectType)}
                            <CodeBlock code={codeSamples.bashShell} language="shellscript" />
                        {:else}
                            <CodeBlock code={codeSamples.powerShell} language="powershell" />
                        {/if}
                        <div class="absolute top-2 right-2">
                            <CopyToClipboardButton value={isBashShell(selectedProjectType) ? codeSamples.bashShell : codeSamples.powerShell} />
                        </div>
                    </div>
                </li>
            {/if}

            {#if isDotNet(selectedProjectType) || isJavaScript(selectedProjectType)}
                <li>
                    {#if isDotNet(selectedProjectType)}
                        <P>Install the NuGet package:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <Code class="block">Install-Package {selectedProjectType.key}</Code>
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={`Install-Package ${selectedProjectType.key}`} />
                            </div>
                        </div>
                    {/if}

                    {#if isJavaScript(selectedProjectType)}
                        {#if !isNode(selectedProjectType)}
                            <P>Install via npm:</P>
                            <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                                <Code class="block">npm install @exceptionless/browser --save</Code>
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value="npm install @exceptionless/browser --save" />
                                </div>
                            </div>
                        {/if}

                        {#if isNode(selectedProjectType)}
                            <P>Install via npm:</P>
                            <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                                <Code class="block">npm install @exceptionless/node --save</Code>
                                <div class="absolute top-2 right-2">
                                    <CopyToClipboardButton value="npm install @exceptionless/node --save" />
                                </div>
                            </div>
                        {/if}
                    {/if}
                </li>
            {/if}

            {#if isJavaScript(selectedProjectType)}
                <li>
                    <P>Configure the ExceptionlessClient with your Exceptionless API key:</P>
                    <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                        {#if !isNode(selectedProjectType)}
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

            {#if isDotNet(selectedProjectType)}
                <li>
                    {#if selectedProjectType.key !== 'Exceptionless' && selectedProjectType.key !== 'Exceptionless.AspNetCore'}
                        <P>Update your {selectedProjectType.config} file with the API key:</P>
                        <div class="relative">
                            <div class="flex items-center">
                                <Input value={apiKey} readonly class="flex-1 font-mono text-sm" />
                                <Button variant="outline" class="ml-2" onclick={copyApiKey}>Copy</Button>
                            </div>
                        </div>
                    {/if}

                    {#if selectedProjectType.key === 'Exceptionless'}
                        <P>Add to your application startup:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <CodeBlock code={codeSamples.exceptionlessDefault} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.exceptionlessDefault} />
                            </div>
                        </div>
                        <P>Then add code to handle unhandled exceptions:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <CodeBlock code={codeSamples.unhandledException} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.unhandledException} />
                            </div>
                        </div>
                    {/if}

                    {#if selectedProjectType.key === 'Exceptionless.AspNetCore'}
                        <P>Add to your application startup:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <CodeBlock code={codeSamples.aspNetCore} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.aspNetCore} />
                            </div>
                        </div>
                    {/if}

                    {#if selectedProjectType.key === 'Exceptionless.Nancy'}
                        <P>Add to your bootstrapper:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <CodeBlock code={codeSamples.nancy} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.nancy} />
                            </div>
                        </div>
                    {/if}

                    {#if selectedProjectType.key === 'Exceptionless.Windows' || selectedProjectType.key === 'Exceptionless.Wpf'}
                        <P>Add to your application startup:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <CodeBlock code={codeSamples.windows} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.windows} />
                            </div>
                        </div>
                    {/if}

                    {#if selectedProjectType.key === 'Exceptionless.WebApi'}
                        <P>Add to your WebApiConfig.Register method:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <CodeBlock code={codeSamples.webApi} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.webApi} />
                            </div>
                        </div>
                        <P>If hosting your Web API in ASP.NET, also add to your Global.asax.cs Application_Start:</P>
                        <div class="bg-muted P-4 relative overflow-x-auto rounded-md">
                            <CodeBlock code={codeSamples.webApiInAspNet} language="csharp" />
                            <div class="absolute top-2 right-2">
                                <CopyToClipboardButton value={codeSamples.webApiInAspNet} />
                            </div>
                        </div>
                    {/if}
                </li>
            {/if}
        {/if}
    </ol>

    {#if selectedProjectType}
        <Alert.Root>
            <Alert.Title>That's it!</Alert.Title>
            <Alert.Description>
                {#if isCommandLine(selectedProjectType)}
                    <P>
                        You can now send data to Exceptionless using the command line. For more information, check out the
                        <A href="https://exceptionless.com/docs/clients/dotnet/sending-events/" target="_blank">documentation</A>
                        for more ways to submit events.
                    </P>
                {/if}
                {#if isDotNet(selectedProjectType)}
                    <P>
                        Your project should now automatically be sending all unhandled exceptions to Exceptionless! For more information, check out the
                        <A href="https://exceptionless.com/docs/clients/dotnet/sending-events/" target="_blank">documentation</A>
                        for more ways to submit events. You can also manually send exceptions using <code>ex.ToExceptionless().Submit()</code>.
                    </P>
                {/if}
                {#if isJavaScript(selectedProjectType)}
                    <P>
                        Your project should now automatically be sending all unhandled exceptions to Exceptionless! For more information, check out the
                        <A href="https://exceptionless.com/docs/clients/javascript/sending-events/" target="_blank">documentation</A>
                        for more ways to submit events. You can also manually send exceptions using
                        <code>await Exceptionless.submitException(ex);</code>.
                    </P>
                {/if}
            </Alert.Description>
        </Alert.Root>
    {/if}
</div>
