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
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(text);
            } else if (!copyUsingTextArea(text)) {
                throw new Error('Clipboard API is not available');
            }

            this.#copiedStatus = 'success';

            this.timeout = setTimeout(() => {
                this.#copiedStatus = undefined;
            }, this.delay);
        } catch {
            this.#copiedStatus = copyUsingTextArea(text) ? 'success' : 'failure';

            this.timeout = setTimeout(() => {
                this.#copiedStatus = undefined;
            }, this.delay);
        }

        return this.#copiedStatus;
    }
}

function copyUsingTextArea(text: string) {
    if (typeof document === 'undefined') {
        return false;
    }

    const activeElement = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
    const textArea = document.createElement('textarea');

    textArea.value = text;
    textArea.setAttribute('readonly', '');
    textArea.style.border = '0';
    textArea.style.height = '1px';
    textArea.style.left = '0';
    textArea.style.opacity = '0';
    textArea.style.padding = '0';
    textArea.style.position = 'fixed';
    textArea.style.top = '0';
    textArea.style.width = '1px';

    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    textArea.setSelectionRange(0, textArea.value.length);

    try {
        return document.execCommand('copy');
    } finally {
        document.body.removeChild(textArea);
        activeElement?.focus();
    }
}
