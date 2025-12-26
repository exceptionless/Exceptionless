<script lang="ts">
    import type { UpdateProject } from '$features/projects/models';

    import { page } from '$app/state';
    import { A, H3, H4, Large, Muted } from '$comp/typography';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import { Separator } from '$comp/ui/separator';
    import { Switch } from '$comp/ui/switch';
    import { deleteProjectConfig, getProjectConfig, getProjectQuery, postProjectConfig, updateProject } from '$features/projects/api.svelte';
    import ProjectLogLevel from '$features/projects/components/project-log-level.svelte';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();
    const projectId = $derived(page.params.projectId || '');
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const update = updateProject({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const projectConfigQuery = getProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const updateProjectConfig = postProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const removeProjectConfig = deleteProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    let dataExclusions = $state('');
    let excludePrivateInformation = $state(false);
    let userNamespaces = $state('');
    let commonMethods = $state('');
    let userAgents = $state('');
    let deleteBotDataEnabled = $state(false);
    const settings = $derived(projectConfigQuery.data?.settings ?? {});

    const dataExclusionsIsDirty = $derived(dataExclusions !== settings['@@DataExclusions']);
    const excludePrivateInformationIsDirty = $derived(excludePrivateInformation !== (settings['@@IncludePrivateInformation'] === 'false'));
    const userNamespacesIsDirty = $derived(userNamespaces !== settings.UserNamespaces);
    const commonMethodsIsDirty = $derived(commonMethods !== settings.CommonMethods);
    const userAgentsIsDirty = $derived(userAgents !== (settings['@@UserAgentBotPatterns'] as string));
    const deleteBotDataEnabledIsDirty = $derived(deleteBotDataEnabled !== projectQuery.data?.delete_bot_data_enabled);

    async function updateOrRemoveProjectConfig(key: string, value: null | string, displayName: string) {
        toast.dismiss(toastId);

        try {
            if (value) {
                await updateProjectConfig.mutateAsync({ key, value });
            } else {
                await removeProjectConfig.mutateAsync({ key });
            }

            toastId = toast.success(`Successfully updated ${displayName} setting.`);
        } catch {
            toastId = toast.error(`Error updating ${displayName}'s setting. Please try again.`);
        }
    }

    async function saveDataExclusion() {
        if (!dataExclusionsIsDirty) {
            return;
        }

        await updateOrRemoveProjectConfig('@@DataExclusions', dataExclusions, 'Data Exclusions');
    }

    async function saveIncludePrivateInformation() {
        if (!excludePrivateInformationIsDirty) {
            return;
        }

        await updateOrRemoveProjectConfig('@@IncludePrivateInformation', excludePrivateInformation ? 'false' : null, 'Exclude Private Information');
    }

    async function saveUserNamespaces() {
        if (!userNamespacesIsDirty) {
            return;
        }

        await updateOrRemoveProjectConfig('UserNamespaces', userNamespaces, 'User Namespaces');
    }

    async function saveCommonMethods() {
        if (!commonMethodsIsDirty) {
            return;
        }

        await updateOrRemoveProjectConfig('CommonMethods', commonMethods, 'Common Methods');
    }

    async function saveUserAgents() {
        if (!userAgentsIsDirty) {
            return;
        }

        await updateOrRemoveProjectConfig('@@UserAgentBotPatterns', userAgents, 'User Agents');
    }

    async function saveDeleteBotDataEnabled() {
        if (!deleteBotDataEnabledIsDirty) {
            return;
        }

        toast.dismiss(toastId);

        try {
            const data: Partial<UpdateProject> = {
                delete_bot_data_enabled: deleteBotDataEnabled
            };
            await update.mutateAsync(data as UpdateProject);

            toastId = toast.success(`Successfully updated Delete Bot Data Enabled setting.`);
        } catch {
            toast.dismiss(toastId);
            toastId = toast.error(`Error updating Delete Bot Data Enabled setting. Please try again.`);
        }
    }

    const debouncedSaveDataExclusion = debounce(500, saveDataExclusion);
    const debouncedSaveIncludePrivateInformation = debounce(500, saveIncludePrivateInformation);
    const debouncedSaveUserNamespaces = debounce(500, saveUserNamespaces);
    const debouncedSaveCommonMethods = debounce(500, saveCommonMethods);
    const debouncedSaveUserAgents = debounce(500, saveUserAgents);
    const debouncedSaveDeleteBotDataEnabled = debounce(500, saveDeleteBotDataEnabled);

    $effect(() => {
        if (projectConfigQuery.dataUpdatedAt) {
            dataExclusions = settings['@@DataExclusions'] ?? '';
            excludePrivateInformation = settings['@@IncludePrivateInformation'] === 'false';
            userNamespaces = settings.UserNamespaces ?? '';
            commonMethods = settings.CommonMethods ?? '';
            userAgents = settings['@@UserAgentBotPatterns'] ?? '';
        }

        if (projectQuery.dataUpdatedAt) {
            deleteBotDataEnabled = projectQuery.data?.delete_bot_data_enabled ?? false;
        }
    });
</script>

<div class="space-y-6">
    <div>
        <H3>Settings</H3>
        <Muted>Create and manage API keys for authenticating your applications with Exceptionless.</Muted>
    </div>
    <Separator />

    <section class="space-y-2">
        <H4>Default Log Level</H4>
        <Muted
            >The default log level controls the minimum log level that should be accepted for log events. Log levels can also be overridden at the log stack
            level.</Muted
        >

        <ProjectLogLevel source="*" {projectId} />
    </section>

    <section class="space-y-2">
        <H4>Data Exclusions</H4>
        <Muted
            >A comma delimited list of field names that should be removed from any error report data (e.g., extended data properties, form fields, cookies and
            query parameters). You can also specify a <A href="https://exceptionless.com/docs/security/" target="_blank">field name with wildcards (*)</A> to specify
            starts with, ends with, or contains just to be extra safe.</Muted
        >
        <Input type="text" placeholder="Example: *Password*, CreditCard*, SSN" bind:value={dataExclusions} onchange={debouncedSaveDataExclusion} />

        <div class="flex items-center space-x-2 pt-1">
            <Switch id="exclude-private-info" bind:checked={excludePrivateInformation} onCheckedChange={debouncedSaveIncludePrivateInformation} />
            <Label for="exclude-private-info">
                Automatically remove user identifiable information from events (e.g., Machine Name, User Information, IP Addresses and more...).
            </Label>
        </div>
    </section>

    <section class="space-y-2">
        <H4>Error Stacking</H4>
        <div class="space-y-4">
            <div class="space-y-2">
                <Large>User Namespaces</Large>
                <Muted
                    >A comma delimited list of the namespace names that your applications code belongs to. If this value is set, only methods inside of these
                    namespaces will be considered as stacking targets.</Muted
                >
                <Input type="text" placeholder="Example: Contoso" bind:value={userNamespaces} onchange={debouncedSaveUserNamespaces} />
            </div>

            <div class="space-y-2">
                <Large>Common Methods</Large>
                <Muted
                    >A comma delimited list of common method names that should not be used as stacking targets. This is useful when your code contains shared
                    utility methods that throw a lot of errors.</Muted
                >
                <Input type="text" placeholder="Example: Assert, Writeline" bind:value={commonMethods} onchange={debouncedSaveCommonMethods} />
            </div>
        </div>
    </section>

    <section class="space-y-2">
        <H4>Spam Detection</H4>
        <Muted>A comma delimited list of user agents that should be ignored.</Muted>
        <Input type="text" placeholder="Example: SpamBot" bind:value={userAgents} onchange={debouncedSaveUserAgents} />

        <div class="flex items-center space-x-2 pt-1">
            <Switch id="delete-bot-data" bind:checked={deleteBotDataEnabled} onCheckedChange={debouncedSaveDeleteBotDataEnabled} />
            <Label for="delete-bot-data">Reduce noise by automatically hiding high volumes of events coming from a single client IP address.</Label>
        </div>
    </section>
</div>
