<script lang="ts">
    import { Muted } from '$comp/typography';
    import OAuthApplicationForm from '$features/admin/components/oauth-applications/oauth-application-form.svelte';
</script>

<div class="max-w-3xl space-y-6">
    <Muted>Create a manually managed public OAuth client.</Muted>
    <OAuthApplicationForm />
</div>
