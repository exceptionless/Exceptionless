<script lang="ts">
    import * as Card from '$comp/ui/card';
    import { Separator } from '$comp/ui/separator';

    import SidebarNav from './(components)/sidebar-nav.svelte';
    import { routes } from './routes';

    let { children } = $props();
</script>

<Card.Root>
    <Card.Title class="p-6 pb-0 text-2xl" tag="h2">Settings</Card.Title>
    <Card.Description class="pl-6">Manage your account settings and set e-mail preferences.</Card.Description>

    <Separator class="mx-6 my-6 w-auto" />

    <Card.Content>
        <div class="flex flex-col space-y-8 lg:flex-row lg:space-x-12 lg:space-y-0">
            <aside class="-mx-4 lg:w-1/5">
                <SidebarNav {routes} />
            </aside>
            <div class="flex-1">
                {@render children()}
            </div>
        </div>
    </Card.Content>
</Card.Root>
