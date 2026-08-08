<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailParityGallery from './EmailParityGallery.svelte';

    const { Story } = defineMeta({
        component: EmailParityGallery,
        tags: ['autodocs'],
        title: 'Email Templates/Parity Gallery'
    });
</script>

<Story name="All Templates" />
