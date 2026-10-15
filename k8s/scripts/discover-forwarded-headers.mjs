import { execFileSync } from 'node:child_process';
import { rmSync, writeFileSync } from 'node:fs';
import { isIP } from 'node:net';
import { pathToFileURL } from 'node:url';

const run = (command, args) => execFileSync(command, args, { encoding: 'utf8', timeout: 60_000 });

// Read the chart's rendered annotations so gateway selection cannot drift from ingress.
function annotation(manifest, name) {
    const values = [...manifest.matchAll(new RegExp(`^\\s+alb\\.networking\\.azure\\.io/${name}:\\s*([a-z0-9.-]+)\\s*$`, 'gm'))]
        .map(match => match[1]);
    if (!values.length || new Set(values).size !== 1)
        throw new Error(`Expected one consistent ingress annotation: ${name}`);
    return values[0];
}

export function discoverForwardedHeaders(release, valuesFile, execute = run) {
    const manifest = execute('helm', ['template', release, 'k8s/exceptionless', '--values', valuesFile, '--show-only', 'templates/api.yaml']);
    const name = annotation(manifest, 'alb-name');
    const namespace = annotation(manifest, 'alb-namespace');
    const gateway = JSON.parse(execute('kubectl', ['get', 'applicationloadbalancers.alb.networking.azure.io', name, '--namespace', namespace, '--output', 'json']));
    const associations = gateway.spec?.associations;
    if (!Array.isArray(associations) || !associations.length)
        throw new Error('Gateway has no association subnet IDs; refusing to infer proxy trust');

    const networks = [];
    for (const id of new Set(associations)) {
        if (typeof id !== 'string' || !/^\/subscriptions\/[^/]+\/resourceGroups\/[^/]+\/providers\/Microsoft\.Network\/virtualNetworks\/[^/]+\/subnets\/[^/]+$/i.test(id))
            throw new Error('Gateway association is not an Azure subnet resource ID');
        const subnet = JSON.parse(execute('az', ['network', 'vnet', 'subnet', 'show', '--ids', id, '--output', 'json', '--only-show-errors']));
        if (subnet.id?.toLowerCase() !== id.toLowerCase() ||
            !subnet.delegations?.some(item => item.serviceName === 'Microsoft.ServiceNetworking/trafficControllers'))
            throw new Error('Association subnet identity or Application Gateway for Containers delegation is missing');

        const prefixes = [subnet.addressPrefix, ...(subnet.addressPrefixes ?? [])].filter(value => value != null);
        if (!prefixes.length)
            throw new Error('Association subnet has no address prefixes');
        for (const prefix of prefixes) {
            const [address, length, extra] = typeof prefix === 'string' ? prefix.split('/') : [];
            const family = isIP(address ?? '');
            if (!family || extra !== undefined || !/^[1-9][0-9]*$/.test(length ?? '') || Number(length) > (family === 4 ? 32 : 128))
                throw new Error(`Invalid or unrestricted association subnet prefix: ${prefix}`);
            networks.push(prefix);
        }
    }

    // A list replaces the previous list on Helm upgrades, including --reuse-values.
    // Individual config-map keys would retain stale higher-numbered entries.
    return { forwardedHeaders: { knownNetworks: [...new Set(networks)].sort() } };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    try {
        const [release, valuesFile, outputFile, ...extra] = process.argv.slice(2);
        if (!release || !valuesFile || !outputFile || extra.length)
            throw new Error('Usage: node k8s/scripts/discover-forwarded-headers.mjs <release> <values-file> <output-json> (from repository root)');
        // A failed refresh must not leave an earlier trust file available for upgrade.
        rmSync(outputFile, { force: true });
        const values = discoverForwardedHeaders(release, valuesFile);
        writeFileSync(outputFile, `${JSON.stringify(values, null, 2)}\n`, { mode: 0o600 });
        console.log(`Discovered ${values.forwardedHeaders.knownNetworks.length} gateway subnet prefix(es)`);
    } catch (error) {
        console.error(`Forwarded-header discovery failed: ${error.message}`);
        process.exitCode = 1;
    }
}
