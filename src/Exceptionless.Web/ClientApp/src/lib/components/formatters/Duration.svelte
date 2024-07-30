<script lang="ts">
    import { getSetIntervalTime } from '$lib/helpers/dates';
    import prettyMilliseconds from 'pretty-ms';

    interface Props {
        /**
         * If the value is a number, it should represent the time difference in milliseconds.
         * If the value is a string or a Date object, it will be parsed to a date and compared to the current time.
         */
        value: Date | number | string | undefined;
    }

    let { value }: Props = $props();

    let durationText = $state('');

    $effect(() => {
        function setDurationText() {
            const options = {
                secondsDecimalDigits: 0,
                verbose: true
            };

            if (typeof value === 'number') {
                durationText = prettyMilliseconds(value, options);
            } else if (value instanceof Date || typeof value === 'string') {
                const time = value instanceof Date ? value.getTime() : new Date(value).getTime();
                durationText = prettyMilliseconds(new Date().getTime() - time, options);
            } else {
                durationText = 'never';
            }
        }

        setDurationText();
        if (!value || typeof value === 'number') {
            return;
        }

        const interval = setInterval(setDurationText, getSetIntervalTime(value));
        return () => clearInterval(interval);
    });

    $effect(() => {
        function setDurationText() {
            if (typeof value === 'number') {
                durationText = prettyMilliseconds(value, {
                    secondsDecimalDigits: 0,
                    verbose: true
                });
            } else if (value instanceof Date || typeof value === 'string') {
                const time = value instanceof Date ? value.getTime() : new Date(value).getTime();
                durationText = prettyMilliseconds(new Date().getTime() - time, {
                    secondsDecimalDigits: 0,
                    verbose: true
                });
            } else {
                durationText = 'never';
            }
        }

        if (value && typeof value !== 'number') {
            setDurationText();
            const interval = setInterval(setDurationText, getSetIntervalTime(value));
            return () => clearInterval(interval);
        } else {
            setDurationText();
        }
    });
</script>

{durationText}
