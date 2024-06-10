<script lang="ts">
    import * as Card from '$comp/ui/card';

    interface Props {
        title: string;
    }

    let { title }: Props = $props();
</script>

<Card.Root>
    <Card.Title tag="h2" class="p-6 pb-4 text-2xl">{title}</Card.Title>
    <Card.Content>
        <div class="w-[200px] border">
            <svg viewBox="0 0 20 20">
                <circle r="10" cx="10" cy="10" fill="white"></circle>
                <circle
                    r="5"
                    cx="10"
                    cy="10"
                    fill="transparent"
                    stroke="tomato"
                    stroke-width="10"
                    stroke-dasharray="calc(35 * 31.4 / 100) 31.4"
                    transform="rotate(-90) translate(-20)"
                ></circle>
            </svg>
        </div>
    </Card.Content>
</Card.Root>
