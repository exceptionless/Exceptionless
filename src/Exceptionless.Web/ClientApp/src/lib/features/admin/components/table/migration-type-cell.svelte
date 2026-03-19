<script lang="ts">
    import { MigrationType } from '$features/admin/models';

    interface Props {
        value: number;
    }

    let { value }: Props = $props();

    function migrationTypeLabel(type: number): string {
        switch (type) {
            case MigrationType.Repeatable:
                return 'Repeatable';
            case MigrationType.VersionedAndResumable:
                return 'Versioned (Resumable)';
            default:
                return 'Versioned';
        }
    }
</script>

<span class="text-xs">{migrationTypeLabel(value)}</span>
