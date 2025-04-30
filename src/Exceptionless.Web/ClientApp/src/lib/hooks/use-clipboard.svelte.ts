type Options = {
    /** The time before the copied status is reset. */
    delay: number;
};

export class UseClipboard {
    get copied() {
        return this.#copiedStatus === 'success';
    }
    get status() {
        return this.#copiedStatus;
    }
    #copiedStatus = $state<'failure' | 'success'>();

    private delay: number;

    private timeout: ReturnType<typeof setTimeout> | undefined = undefined;

    constructor({ delay = 500 }: Partial<Options> = {}) {
        this.delay = delay;
    }

    async copy(text: string) {
        if (this.timeout) {
            this.#copiedStatus = undefined;
            clearTimeout(this.timeout);
        }

        try {
            await navigator.clipboard.writeText(text);

            this.#copiedStatus = 'success';

            this.timeout = setTimeout(() => {
                this.#copiedStatus = undefined;
            }, this.delay);
        } catch {
            this.#copiedStatus = 'failure';

            this.timeout = setTimeout(() => {
                this.#copiedStatus = undefined;
            }, this.delay);
        }

        return this.#copiedStatus;
    }
}
