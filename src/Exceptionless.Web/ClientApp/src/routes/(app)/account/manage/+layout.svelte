<script lang="ts">
	import * as Card from '$comp/ui/card';
	import { Separator } from '$comp/ui/separator';
	import SidebarNav from './(components)/sidebar-nav.svelte';

	const sidebarNavItems = [
		{
			title: 'Account',
			href: '/next/account/manage'
		},
		{
			title: 'Appearance',
			href: '/next/account/manage/appearance'
		},
		{
			title: 'Notifications',
			href: '/next/account/manage/notifications'
		}
	];
</script>

<svelte:head>
	<title>My Account - Exceptionless</title>
</svelte:head>

<Card.Root>
	<Card.Title tag="h2" class="p-6 pb-0 text-2xl">Settings</Card.Title>
	<Card.Description class="pl-6"
		>Manage your account settings and set e-mail preferences.</Card.Description
	>

	<Separator class="w-auto mx-6 my-6" />

	<Card.Content>
		<div class="flex flex-col space-y-8 lg:flex-row lg:space-x-12 lg:space-y-0">
			<aside class="-mx-4 lg:w-1/5">
				<SidebarNav items={sidebarNavItems} />
			</aside>
			<div class="flex-1">
				<slot />
			</div>
		</div>
	</Card.Content>
</Card.Root>
