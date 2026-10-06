import { describe, expect, it } from 'vitest';

import { AssistantSettingsSchema } from './schemas';

describe('assistant settings schema', () => {
    it('accepts the GLM 5.3 Flash OpenRouter model ID', () => {
        const result = AssistantSettingsSchema.safeParse({ model: 'z-ai/glm-5.3-flash' });

        expect(result.success).toBe(true);
    });

    it('rejects an empty model ID', () => {
        const result = AssistantSettingsSchema.safeParse({ model: '   ' });

        expect(result.success).toBe(false);
    });
});
