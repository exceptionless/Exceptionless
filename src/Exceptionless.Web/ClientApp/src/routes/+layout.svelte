<script>
	import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
	import '../app.css';

	// Create a client
	const queryClient = new QueryClient();
</script>

<QueryClientProvider client={queryClient}>
	<slot />
</QueryClientProvider>
