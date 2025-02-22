<script module>
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import { fn } from '@storybook/test';

    import Button from './Button.svelte';

    // More on how to set up stories at: https://storybook.js.org/docs/writing-stories
    const { Story } = defineMeta({
        args: {
            onClick: fn()
        },
        argTypes: {
            backgroundColor: { control: 'color' },
            size: {
                control: { type: 'select' },
                options: ['small', 'medium', 'large']
            }
        },
        component: Button,
        tags: ['autodocs'],
        title: 'Example/Button'
    });
</script>

<!-- More on writing stories with args: https://storybook.js.org/docs/writing-stories/args -->
<Story name="Primary" args={{ label: 'Button', primary: true }} />

<Story name="Secondary" args={{ label: 'Button' }} />

<Story name="Large" args={{ label: 'Button', size: 'large' }} />

<Story name="Small" args={{ label: 'Button', size: 'small' }} />
