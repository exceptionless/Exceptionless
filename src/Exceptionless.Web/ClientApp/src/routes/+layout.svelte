<script lang="ts">
	import { setDefaultBaseUrl, setAccessTokenStore } from '$api/FetchClient';
	import { accessToken } from '$api/auth';
	import '../app.css';

	setDefaultBaseUrl('api/v2');
	setAccessTokenStore(accessToken);
</script>

<slot />
