import { describe, expect, it } from 'vitest';

import { filterUsesPremiumFeatures } from '../../premium-filter';
import { deserializeFilters, getProjectScopeFilter, serializeFilters, toFilter } from './helpers.svelte';
import { EnvironmentFilter, KeywordFilter, ProjectFilter, TypeFilter } from './models.svelte';

describe('EnvironmentFilter', () => {
    it('normalizes names and supports all, multiple, and unspecified environments', () => {
        expect(new EnvironmentFilter().toFilter()).toBe('');
        expect(new EnvironmentFilter([' Production ', 'production']).toFilter()).toBe('environment:production');
        expect(new EnvironmentFilter(['production', 'staging']).toFilter()).toBe('(environment:production OR environment:staging)');
        expect(new EnvironmentFilter(['']).toFilter()).toBe('_missing_:environment');
        expect(new EnvironmentFilter(['production', '']).toFilter()).toBe('(environment:production OR _missing_:environment)');
    });

    it('quotes custom values and combines the environment choice with other filters', () => {
        expect(toFilter([new ProjectFilter(['project-1']), new EnvironmentFilter(['qa west'])])).toContain('environment:"qa west"');
        expect(new EnvironmentFilter(['qa:west']).toFilter()).toBe('environment:"qa:west"');
        const name = '"qa" OR _exists_:message';
        expect(new EnvironmentFilter([name]).toFilter()).toBe(`environment:${JSON.stringify(name.toLowerCase())}`);
    });

    it('preserves selections and hidden state through saved-view and URL serialization', () => {
        const filter = new EnvironmentFilter(['production', '']);
        filter.hidden = true;
        const restored = deserializeFilters(serializeFilters([filter]));
        expect(restored[0]).toBeInstanceOf(EnvironmentFilter);
        expect(restored[0]?.hidden).toBe(true);
        expect(toFilter(restored)).toBe(filter.toFilter());
        const clone = filter.clone();
        clone.value.push('staging');
        expect(filter.value).toEqual(['production', '']);
    });

    it('scopes environment discovery to picked projects and project terms in a raw search', () => {
        const first = '537650f3b77efe23a47914f3';
        const second = '537650f3b77efe23a47914f4';
        const third = '537650f3b77efe23a47914f5';

        expect(getProjectScopeFilter([new TypeFilter(['error'])])).toBe('');
        expect(getProjectScopeFilter([new ProjectFilter([first]), new EnvironmentFilter(['production'])])).toBe(`project:${first}`);
        expect(getProjectScopeFilter([new KeywordFilter(`type:error project:${first}`)])).toBe(`project:${first}`);
        expect(getProjectScopeFilter([new KeywordFilter(`(project:${first} OR project_id:${second})`)])).toBe(`(project:${first} OR project:${second})`);
        expect(getProjectScopeFilter([new KeywordFilter(`project:(${first} OR ${second})`)])).toBe(`(project:${first} OR project:${second})`);
        expect(getProjectScopeFilter([new KeywordFilter(`-project:${first} !project:${second} NOT project:${third}`)])).toBe('');
        expect(getProjectScopeFilter([new ProjectFilter([first]), new KeywordFilter(`project:${second}`)])).toBe(`(project:${first} OR project:${second})`);
        expect(getProjectScopeFilter([new KeywordFilter(`NOT (project:${first}) -(type:error OR project:${second}) !(project:${third})`)])).toBe('');
        expect(getProjectScopeFilter([new KeywordFilter(`NOT (status:fixed) (project:${first} OR -(project:${second})) "a (" project:${third}`)])).toBe(
            `(project:${first} OR project:${third})`
        );
    });

    it('allows environment searches on free plans and keeps custom data premium', () => {
        for (const resource of ['event', 'event-stack'] as const) {
            expect(filterUsesPremiumFeatures('environment:production', resource)).toBe(false);
            expect(filterUsesPremiumFeatures('_missing_:environment', resource)).toBe(false);
            expect(filterUsesPremiumFeatures('environment:production data.customer:123', resource)).toBe(true);
        }
    });
});
