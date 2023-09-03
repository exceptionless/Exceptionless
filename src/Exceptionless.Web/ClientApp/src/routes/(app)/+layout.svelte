<script lang="ts">
	import { useQueryClient } from '@tanstack/svelte-query';
	import { accessToken, gotoLogin, isAuthenticated } from '$api/auth';
	import { WebSocketClient } from '$lib/api/WebSocketClient';
	import { isEntityChangedType, type WebSocketMessageType } from '$lib/models/api';
	import {
		setAccessTokenStore,
		setDefaultModelValidator,
		useGlobalMiddleware
	} from '$api/FetchClient';
	import { validate } from '$lib/validation/validation';

	isAuthenticated.subscribe((authenticated) => {
		if (!authenticated) gotoLogin();
	});

	setDefaultModelValidator(validate);
	setAccessTokenStore(accessToken);
	useGlobalMiddleware(async (ctx, next) => {
		await next();
		if (ctx.response && ctx.response.status === 401) await gotoLogin();
	});

	const queryClient = useQueryClient();
	const ws = new WebSocketClient();

	ws.onMessage = async (message) => {
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
			await queryClient.invalidateQueries([data.message.type]);
		}

		// This event is fired when a user is added or removed from an organization.
		// if (data.type === "UserMembershipChanged" && data.message?.organization_id) {
		//     $rootScope.$emit("OrganizationChanged", data.message);
		//     $rootScope.$emit("ProjectChanged", data.message);
		// }
	};
	ws.onError = (error) => console.error({ 'WS Error': error });
</script>

{#if $isAuthenticated}
	<div class="navbar bg-base-100">
		<div class="flex-1">
			<a href="/" class="btn btn-ghost text-xl normal-case">Exceptionless</a>
		</div>
		<div class="flex-none gap-2">
			<div class="form-control">
				<input
					type="text"
					placeholder="Search"
					class="input input-bordered w-24 md:w-auto"
				/>
			</div>
			<div class="dropdown dropdown-end">
				<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
				<div tabindex="0" class="avatar btn btn-circle btn-ghost">
					<div class="w-10 rounded-full">
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
					<li><a href="/logout">Logout</a></li>
				</ul>
			</div>
		</div>
	</div>

	<div class="m-5">
		<slot />
	</div>

	<footer class="footer items-center bg-base-300 p-4 text-base-content">
		<div class="grid-flow-col items-center">
			<p>
				© 2023
				<a href="https://exceptionless.com" target="_blank" class="link">Exceptionless</a>
				<a href="https://exceptionless.com/news/" target="_blank" class="link ml-2">News</a>
				<a href="https://exceptionless.com/terms/" target="_blank" class="link ml-2"
					>Terms of Use</a
				>
				<a href="https://exceptionless.com/privacy/" target="_blank" class="link ml-2"
					>Privacy Policy</a
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
{/if}
