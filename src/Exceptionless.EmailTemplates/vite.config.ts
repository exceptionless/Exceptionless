import { spawn } from 'node:child_process';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import { defineConfig, type Plugin } from 'vite';

const projectDirectory = dirname(fileURLToPath(import.meta.url));

function watchEmailTemplates(): Plugin {
    return {
        name: 'watch-email-templates',
        configureServer(server) {
            const npmExecutable = process.env.npm_execpath;
            if (!npmExecutable) {
                server.config.logger.warn('Email template watch skipped because npm_execpath is not set.');
                return;
            }

            const templateBuilder = spawn(process.execPath, [npmExecutable, 'run', 'dev'], {
                cwd: projectDirectory,
                stdio: 'inherit'
            });
            const stopTemplateBuilder = () => templateBuilder.kill();
            server.httpServer?.once('close', stopTemplateBuilder);
        }
    };
}

// Storybook loads the default Vite configuration. Keep production-template
// generation in vite.email.config.ts so preview builds cannot mutate Core files.
export default defineConfig({
    plugins: [svelte(), watchEmailTemplates()]
});
