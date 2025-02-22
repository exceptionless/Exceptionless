<script lang="ts">
    import './button.css';

    interface Props {
        /** What background color to use */
        backgroundColor?: string;
        /** Button contents */
        label: string;
        /** The onclick event handler */
        onClick?: () => void;
        /** Is this the principal call to action on the page? */
        primary?: boolean;
        /** How large should the button be? */
        size?: 'large' | 'medium' | 'small';
    }

    const { backgroundColor, label, onClick, primary = false, size = 'medium' }: Props = $props();
</script>

<button
    type="button"
    class={['storybook-button', `storybook-button--${size}`].join(' ')}
    class:storybook-button--primary={primary}
    class:storybook-button--secondary={!primary}
    style:background-color={backgroundColor}
    onclick={onClick}
>
    {label}
</button>
