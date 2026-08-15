<script lang="ts">
    import * as Avatar from './index';
</script>

<Avatar.Root data-testid="circle-avatar">
    <Avatar.Image alt="Circle" src="/circle.png" />
    <Avatar.Fallback>CI</Avatar.Fallback>
</Avatar.Root>

<Avatar.Root data-testid="square-avatar" shape="square">
    <Avatar.Image alt="Square" src="/square.png" />
    <Avatar.Fallback>SQ</Avatar.Fallback>
</Avatar.Root>
