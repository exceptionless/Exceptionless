<script lang="ts">
    import * as Card from '$comp/ui/card';

    interface Props {
        title: string;
    }

    let { title }: Props = $props();
</script>

<Card.Root>
    <Card.Header>
        <Card.Title class="text-2xl" level={2}>{title}</Card.Title>
    </Card.Header>
    <Card.Content>
        <div class="w-[200px] border">
            <svg viewBox="0 0 20 20">
                <circle cx="10" cy="10" fill="white" r="10"></circle>
                <circle
                    cx="10"
                    cy="10"
                    fill="transparent"
                    r="5"
                    stroke="tomato"
                    stroke-dasharray="calc(35 * 31.4 / 100) 31.4"
                    stroke-width="10"
                    transform="rotate(-90) translate(-20)"
                ></circle>
            </svg>
        </div>
    </Card.Content>
</Card.Root>
