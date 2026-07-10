// @ts-check
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { basename, dirname, join, normalize } from 'node:path';
import { pathToFileURL } from 'node:url';

function deriveScope() {
    const gitDir = git(['rev-parse', '--git-dir']);
    const normalizedGitDir = gitDir ? normalize(gitDir).replace(/[\\/]+$/, '') : '';
    const gitRoot = findGitRoot();
    const worktreeName = /[\\/]worktrees[\\/]/i.test(normalizedGitDir) ? basename(normalizedGitDir) : gitRoot.worktreeName;
    const name = worktreeName || basename(git(['rev-parse', '--show-toplevel']) || gitRoot.root || 'Exceptionless');

    return (
        name
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '') || 'exceptionless'
    );
}

function findGitRoot() {
    for (let dir = process.cwd(); ; dir = dirname(dir)) {
        const dotGit = join(dir, '.git');
        if (existsSync(dotGit)) {
            if (statSync(dotGit).isFile()) {
                const content = readFileSync(dotGit, 'utf8').trim();
                const gitDir = content.replace(/^gitdir:\s*/i, '').trim();
                return {
                    root: dir,
                    worktreeName: basename(normalize(gitDir).replace(/[\\/]+$/, ''))
                };
            }

            return { root: dir, worktreeName: '' };
        }

        const parent = dirname(dir);
        if (parent === dir) {
            return { root: '', worktreeName: '' };
        }
    }
}

function git(args) {
    try {
        return execFileSync('git', args, { encoding: 'utf8' }).trim();
    } catch {
        return '';
    }
}

const isAppResource = (resource) => /^app$/i.test(resource.displayName ?? '') || /^app$/i.test(resource.name ?? '');
const isOldAppResource = (resource) => /^oldapp$/i.test(resource.displayName ?? '') || /^oldapp$/i.test(resource.name ?? '');
const isApiResource = (resource) => /^api$/i.test(resource.displayName ?? '') || /^api$/i.test(resource.name ?? '');

export function resolveUrls() {
    const clean = (url) => url && url.replace(/\/$/, '');
    const scope = deriveScope();
    const { apiUrl, appUrl, found, oldAppUrl } = fromAspire();

    if (appUrl) {
        return {
            apiUrl: clean(apiUrl),
            appUrl: clean(appUrl),
            oldAppUrl: clean(oldAppUrl),
            scope,
            status: 'ready'
        };
    }

    if (found) {
        return {
            message: "The app is starting up; its Svelte frontend isn't ready yet. Wait with `aspire wait App`.",
            scope,
            status: 'starting'
        };
    }

    return {
        message: 'No running app found. Start it with `aspire run`, then run `npm run urls` again.',
        scope,
        status: 'no-app'
    };
}

function fromAspire() {
    const root = git(['rev-parse', '--show-toplevel']) || findGitRoot().root;
    let raw;

    try {
        raw = execFileSync(
            'aspire',
            ['describe', '--apphost', join(root, 'src', 'Exceptionless.AppHost', 'Exceptionless.AppHost.csproj'), '--non-interactive', '--format', 'Json'],
            { cwd: root || undefined, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], timeout: 60_000 }
        );
    } catch {
        return { found: false };
    }

    const start = raw.indexOf('{');
    const end = raw.lastIndexOf('}');
    if (start === -1 || end === -1) {
        return { found: false };
    }

    let resources;
    try {
        resources = JSON.parse(raw.slice(start, end + 1)).resources ?? [];
    } catch {
        return { found: false };
    }

    let found = false;
    let appUrl;
    let oldAppUrl;
    let apiUrl;

    for (const resource of resources) {
        const app = isAppResource(resource);
        const oldApp = isOldAppResource(resource);
        const api = isApiResource(resource);
        if (!app && !oldApp && !api) {
            continue;
        }

        if (app) {
            found = true;
        }

        if (resource.state && resource.state !== 'Running') {
            continue;
        }

        const urls = (resource.urls ?? []).filter((url) => /^https?:\/\//i.test(url.url ?? ''));
        if (app) {
            appUrl ??= (urls.find((url) => /open app$/i.test(url.displayName ?? '')) ?? urls[0])?.url;
        } else if (oldApp) {
            oldAppUrl ??= (urls.find((url) => /old/i.test(url.displayName ?? '')) ?? urls[0])?.url;
        } else if (api) {
            apiUrl ??= (urls.find((url) => /open api|api/i.test(url.displayName ?? '')) ?? urls[0])?.url;
        }
    }

    return { apiUrl, appUrl, found, oldAppUrl };
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
    process.stdout.write(`${JSON.stringify(resolveUrls(), null, 2)}\n`);
}
