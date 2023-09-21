<script lang="ts">
	import { accessToken, gotoLogin, isAuthenticated } from '$api/auth';
	import { WebSocketClient } from '$lib/api/WebSocketClient';
	import { isEntityChangedType, type WebSocketMessageType } from '$lib/models/api';
	import { setDefaultModelValidator, useGlobalMiddleware } from '$api/FetchClient';
	import { validate } from '$lib/validation/validation';
	import { onMount } from 'svelte';

	import logo from '$lib/assets/exceptionless-logo.png';
	import { drawerComponent, showDrawer } from '$lib/stores/drawer';

	isAuthenticated.subscribe(async (authenticated) => {
		if (!authenticated) {
			await gotoLogin();
		}
	});

	setDefaultModelValidator(validate);
	useGlobalMiddleware(async (ctx, next) => {
		await next();
		if (ctx.response && ctx.response.status === 401) accessToken.set(null);
	});

	async function onMessage(message: MessageEvent) {
		const data: { type: WebSocketMessageType; message: unknown } = message.data
			? JSON.parse(message.data)
			: null;

		if (!data?.type) {
			return;
		}

		document.dispatchEvent(
			new CustomEvent(data.type, {
				detail: data.message,
				bubbles: true
			})
		);

		if (isEntityChangedType(data)) {
			//await queryClient.invalidateQueries([data.message.type]);
		}

		// This event is fired when a user is added or removed from an organization.
		// if (data.type === "UserMembershipChanged" && data.message?.organization_id) {
		//     $rootScope.$emit("OrganizationChanged", data.message);
		//     $rootScope.$emit("ProjectChanged", data.message);
		// }
	}

	onMount(() => {
		if (!$isAuthenticated) return;

		const ws = new WebSocketClient();
		ws.onMessage = onMessage;

		return () => {
			ws?.close();
		};
	});

	const currentYear = new Date().getFullYear();
</script>

{#if $isAuthenticated}
	<div class="drawer drawer-end">
		<input id="app-drawer" type="checkbox" class="drawer-toggle" bind:checked={$showDrawer} />
		<div class="drawer-content">
			<div class="flex flex-col h-screen justify-between" data-theme="light">
				<header
					class="navbar min-h-[40px] h-[52px] bg-base-300 border-primary border-b-2 drop-shadow-lg sticky top-0 py-1 px-0"
					data-theme="dark"
				>
					<div class="flex-1 pl-[20px]">
						<a href="/" class="text-xl normal-case"
							><img src={logo} class="h-[38px]" alt="Exceptionless" /></a
						>
					</div>
					<div class="flex-none gap-2">
						<div class="form-control">
							<input
								type="text"
								placeholder="Search"
								class="input input-bordered h-[38px] w-24 md:w-auto"
							/>
						</div>
						<div class="dropdown dropdown-end">
							<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
							<div tabindex="0" class="avatar btn btn-square btn-ghost">
								<div class="w-10 rounded">
									<img
										src="//www.gravatar.com/avatar/89b10deee628535a5510db131f983541?default=mm&size=100"
										alt="avatar"
									/>
								</div>
							</div>
							<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
							<ul
								tabindex="0"
								class="menu dropdown-content rounded-box menu-sm z-[1] mt-3 w-52 bg-base-100 p-2 shadow"
							>
								<li>
									<a href="/account/manage" class="justify-between">
										Profile
										<span class="badge">New</span>
									</a>
								</li>
								<li><a href="/account/manage">Settings</a></li>
								<li><a href="/next/logout">Logout</a></li>
							</ul>
						</div>
					</div>
				</header>

				<main class="mb-auto h-10 m-5 min-h-8">
					<slot />
				</main>

				<footer
					class="footer h-10 items-center bg-base-300 p-2 text-base-content sticky bottom-0 border-t border-gray-300"
				>
					<div class="grid-flow-col items-center">
						<p>
							© {currentYear}
							<a href="https://exceptionless.com" target="_blank" class="link"
								>Exceptionless</a
							>
							<a
								href="https://exceptionless.com/news/"
								target="_blank"
								class="link ml-2">News</a
							>
							<a
								href="https://exceptionless.com/terms/"
								target="_blank"
								class="link ml-2">Terms of Use</a
							>
							<a
								href="https://exceptionless.com/privacy/"
								target="_blank"
								class="link ml-2">Privacy Policy</a
							>
						</p>
					</div>
					<div class="grid-flow-col gap-4 md:place-self-center md:justify-self-end">
						<a
							href="https://github.com/exceptionless/Exceptionless/releases"
							target="_blank"
							title="Version">9.0.0-TODO</a
						>
					</div>
				</footer>
			</div>
		</div>
		<div class="drawer-side">
			<label for="app-drawer" class="drawer-overlay"></label>
			<div class="menu p-4 w-80 min-h-full bg-base-200 text-base-content">
				{#if drawerComponent}
					<svelte:component this={$drawerComponent} />
				{:else}
					<p>$drawerComponent is undefined</p>
				{/if}
			</div>
		</div>
	</div>
{/if}
