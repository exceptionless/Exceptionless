import js from '@eslint/js';
import stylistic from '@stylistic/eslint-plugin';
import globals from 'globals';

export default [
    {
        files: ['tests/ci/**/*.js', 'k8s/scripts/**/*.js'],
        languageOptions: {
            globals: globals.node
        },
        plugins: {
            '@stylistic': stylistic
        },
        rules: {
            ...js.configs.recommended.rules,
            '@stylistic/brace-style': ['error', '1tbs', { allowSingleLine: false }],
            curly: ['error', 'all'],
            'padding-line-between-statements': ['error', { blankLine: 'always', next: '*', prev: 'block-like' }]
        }
    }
];
