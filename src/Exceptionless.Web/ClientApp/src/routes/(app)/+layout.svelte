<script lang="ts">
	import { page } from '$app/stores';

	import NavbarLayout from '$lib/layouts/Navbar.svelte';
	import SidebarLayout from '$lib/layouts/Sidebar.svelte';
	import FooterLayout from '$lib/layouts/Footer.svelte';
	import { isSidebarOpen, isSmallScreen } from '$lib/stores/sidebar';

	import { accessToken, gotoLogin, isAuthenticated } from '$api/auth';
	import { WebSocketClient } from '$lib/api/WebSocketClient';
	import { isEntityChangedType, type WebSocketMessageType } from '$lib/models/websocket';
	import { setDefaultModelValidator, useGlobalMiddleware } from '$api/FetchClient';
	import { validate } from '$lib/validation/validation';
	import { onMount } from 'svelte';

	import { useQueryClient } from '@tanstack/svelte-query';

	isAuthenticated.subscribe(async (authenticated) => {
		if (!authenticated) {
			await gotoLogin();
		}
	});

	setDefaultModelValidator(validate);
	useGlobalMiddleware(async (ctx, next) => {
		await next();

		if (ctx.response && ctx.response.status === 401) {
			accessToken.set(null);
			return;
		}

		if (ctx.response?.headers.has('X-Result-Count') && ctx.response?.data !== null) {
			const resultCountHeaderValue = parseInt(
				ctx.response.headers.get('X-Result-Count') || ''
			);

			ctx.response.meta.total = resultCountHeaderValue;

			if (
				typeof ctx.response?.data === 'object' &&
				(ctx.response.data as { resultCount?: number | null }).resultCount === undefined
			) {
				(ctx.response.data as { resultCount?: number | null }).resultCount = !isNaN(
					resultCountHeaderValue
				)
					? resultCountHeaderValue
					: null;
			}
		}
	});

	const queryClient = useQueryClient();
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
			await queryClient.invalidateQueries({ queryKey: [data.message.type] });
		}

		// This event is fired when a user is added or removed from an organization.
		// if (data.type === "UserMembershipChanged" && data.message?.organization_id) {
		//     $rootScope.$emit("OrganizationChanged", data.message);
		//     $rootScope.$emit("ProjectChanged", data.message);
		// }
	}

	// Close Sidebar on page change on mobile
	page.subscribe(() => {
		if ($isSmallScreen === true) {
			isSidebarOpen.set(false);
		}
	});

	onMount(() => {
		if (!$isAuthenticated) return;

		const ws = new WebSocketClient();
		ws.onMessage = onMessage;
		ws.onOpen = (_, isReconnect) => {
			if (isReconnect) {
				document.dispatchEvent(
					new CustomEvent('refresh', {
						detail: 'WebSocket Connected',
						bubbles: true
					})
				);
			}
		};

		return () => {
			ws?.close();
		};
	});
</script>

{#if $isAuthenticated}
	<NavbarLayout></NavbarLayout>
	<div class="flex overflow-hidden pt-16">
		<SidebarLayout />

		<div
			class="relative h-full w-full overflow-y-auto bg-secondary text-secondary-foreground {$isSidebarOpen
				? 'lg:ml-64'
				: 'lg:ml-16'}"
		>
			<main>
				<div class="px-4 pt-6">
					<slot />
				</div>
			</main>

			<FooterLayout></FooterLayout>
		</div>
	</div>
{/if}
