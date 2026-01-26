#!/usr/bin/env node

import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { generateApi } from 'swagger-typescript-api';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, '..');
const outputDir = path.resolve(rootDir, 'src/lib/generated');

// SWAGGER_URL can be an HTTP URL or a file path (for regenerating from baseline during development)
const swaggerSource = process.env.SWAGGER_URL || 'http://localhost:5200/docs/v2/openapi.json';
const isLocalFile = swaggerSource.startsWith('/') || swaggerSource.startsWith('.');

if (isLocalFile && !existsSync(swaggerSource)) {
    console.error(`Error: File not found: ${swaggerSource}`);
    process.exit(1);
}

const FILE_PREFIX_PATTERN = /^\/\* eslint-disable \*\/\n\/\* tslint:disable \*\/\n\/\/ @ts-nocheck\n\/\*[\s\S]*?\*\/\n\n/;

async function generate() {
    console.log('Generating API types and schemas from:', swaggerSource, isLocalFile ? '(local file)' : '(URL)');

    try {
        const result = await generateApi({
            addReadonly: false,
            codeGenConstructs: (struct) => ({
                ...struct
            }),
            extraTemplates: [
                {
                    name: 'schemas',
                    path: path.resolve(rootDir, 'api-templates/schemas.ejs')
                }
            ],
            fileName: 'api.ts',
            generateClient: false,
            output: outputDir,
            primitiveTypeConstructs: (struct) => ({
                ...struct
            }),
            silent: false,
            templates: path.resolve(rootDir, 'api-templates'),
            toJS: false,
            typeSuffix: '',
            ...(isLocalFile ? { input: path.resolve(swaggerSource) } : { url: swaggerSource })
        });

        console.log('Generated files:');
        for (const file of result.files) {
            const fileName = `${file.fileName}${file.fileExtension}`;
            console.log(`  - ${fileName}`);

            const filePath = path.join(outputDir, fileName);
            stripFilePrefix(filePath);
        }

        console.log('\nAPI generation complete!');
    } catch (error) {
        console.error('Error generating API:', error);
        process.exit(1);
    }
}

function stripFilePrefix(filePath) {
    const content = readFileSync(filePath, 'utf-8');
    const cleaned = content.replace(FILE_PREFIX_PATTERN, '');
    writeFileSync(filePath, cleaned);
}

generate();
