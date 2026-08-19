import { describe, expect, it } from 'vitest';

import { preserveDetailSheetForAssistant } from './detail-sheet-interaction';

describe('preserveDetailSheetForAssistant', () => {
    it('prevents a nested assistant-trigger interaction from dismissing the detail sheet', () => {
        const trigger = document.createElement('button');
        const icon = document.createElement('span');
        trigger.dataset.assistantTrigger = '';
        trigger.append(icon);
        document.body.append(trigger);
        const event = new PointerEvent('pointerdown', { bubbles: true, cancelable: true });
        icon.addEventListener('pointerdown', preserveDetailSheetForAssistant);

        icon.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(true);
        trigger.remove();
    });

    it('does not prevent an ordinary outside interaction', () => {
        const outsideButton = document.createElement('button');
        document.body.append(outsideButton);
        const event = new PointerEvent('pointerdown', { bubbles: true, cancelable: true });
        outsideButton.addEventListener('pointerdown', preserveDetailSheetForAssistant);

        outsideButton.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(false);
        outsideButton.remove();
    });
});
