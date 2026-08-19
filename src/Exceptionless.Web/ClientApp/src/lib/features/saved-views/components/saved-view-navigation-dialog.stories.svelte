<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import SavedViewNavigationDialog from './saved-view-navigation-dialog.svelte';

    const { Story } = defineMeta({
        component: SavedViewNavigationDialog,
        tags: ['autodocs'],
        title: 'Components/Saved Views/SavedViewNavigationDialog'
    });
</script>

<Story
    name="Unsaved Changes"
    args={{
        onDiscard: () => {},
        onSave: () => {},
        onStay: () => {},
        open: true,
        saving: false
    }}
/>
