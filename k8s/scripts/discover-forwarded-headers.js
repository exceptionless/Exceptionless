import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { realpathSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { isIP } from 'node:net';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const APPLICATION_LOAD_BALANCER_RESOURCE = 'applicationloadbalancers.alb.networking.azure.io';
const ASSOCIATION_SUBNET_ID =
    /^\/subscriptions\/[^/]+\/resourceGroups\/[^/]+\/providers\/Microsoft\.Network\/virtualNetworks\/[^/]+\/subnets\/[^/]+$/i;
const HELM_RELEASE_NAME = /^[a-z0-9](?:[-a-z0-9]*[a-z0-9])?(?:\.[a-z0-9](?:[-a-z0-9]*[a-z0-9])?)*$/;
const MAX_HELM_RELEASE_NAME_LENGTH = 53;
const TRAFFIC_CONTROLLER_DELEGATION = 'Microsoft.ServiceNetworking/trafficControllers';
const USAGE =
    'Usage: node k8s/scripts/discover-forwarded-headers.js <release> <values-file> <output-json> (from repository root)';

function discoverKnownNetworks(release, valuesFile) {
    const manifest = run('helm', [
        'template',
        release,
        'k8s/exceptionless',
        '--values',
        valuesFile,
        '--show-only',
        'templates/api.yaml'
    ]);
    const gatewayName = readIngressAnnotation(manifest, 'alb-name');
    const gatewayNamespace = readIngressAnnotation(manifest, 'alb-namespace');
    const subnetIds = readAssociationSubnetIds(gatewayName, gatewayNamespace);
    const networks = subnetIds.flatMap(subnetId => readSubnetPrefixes(subnetId));

    // A list replaces the previous list on Helm upgrades, including --reuse-values.
    // Individual config-map keys would retain stale higher-numbered entries.
    return [...new Set(networks)].sort();
}

function main() {
    const [release, valuesFile, outputFile, ...extra] = process.argv.slice(2);

    if (!release || !valuesFile || !outputFile || extra.length > 0) {
        throw new Error(USAGE);
    }

    if (release.length > MAX_HELM_RELEASE_NAME_LENGTH || !HELM_RELEASE_NAME.test(release)) {
        throw new Error('The Helm release name is invalid');
    }

    if (pathsReferToSameFile(valuesFile, outputFile)) {
        throw new Error('The output file must be different from the input values file');
    }

    // A failed refresh must not leave an earlier trust file available for upgrade.
    rmSync(outputFile, { force: true });

    const knownNetworks = discoverKnownNetworks(release, valuesFile);
    const values = { forwardedHeaders: { knownNetworks } };
    writeValuesFile(outputFile, values);
    console.log(`Discovered ${knownNetworks.length} gateway subnet prefix(es)`);
}

function pathsReferToSameFile(firstPath, secondPath) {
    if (resolve(firstPath) === resolve(secondPath)) {
        return true;
    }

    try {
        return realpathSync(firstPath) === realpathSync(secondPath);
    } catch (error) {
        if (error.code === 'ENOENT') {
            return false;
        }

        throw error;
    }
}

function readAssociationSubnetIds(gatewayName, gatewayNamespace) {
    const gateway = runJson(
        'kubectl',
        ['get', APPLICATION_LOAD_BALANCER_RESOURCE, gatewayName, '--namespace', gatewayNamespace, '--output', 'json'],
        `ApplicationLoadBalancer ${gatewayNamespace}/${gatewayName}`
    );

    if (!gateway || typeof gateway !== 'object' || Array.isArray(gateway)) {
        throw new Error(`ApplicationLoadBalancer ${gatewayNamespace}/${gatewayName} returned an invalid object`);
    }

    const associations = gateway.spec?.associations;

    if (!Array.isArray(associations) || associations.length === 0) {
        throw new Error('Gateway has no association subnet IDs; refusing to infer proxy trust');
    }

    const hasInvalidAssociation = associations.some(
        association => typeof association !== 'string' || !ASSOCIATION_SUBNET_ID.test(association)
    );

    if (hasInvalidAssociation) {
        throw new Error('Gateway contains an invalid Azure association subnet resource ID');
    }

    return [...new Set(associations)];
}

// Read the chart's rendered annotations so gateway selection cannot drift from ingress.
function readIngressAnnotation(manifest, annotationName) {
    const annotationPattern = new RegExp(
        `^\\s+alb\\.networking\\.azure\\.io/${annotationName}:\\s*([a-z0-9.-]+)\\s*$`,
        'gm'
    );
    const values = [...new Set([...manifest.matchAll(annotationPattern)].map(match => match[1]))];

    if (values.length === 0) {
        throw new Error(`Rendered API ingress is missing the ${annotationName} annotation`);
    }

    if (values.length > 1) {
        throw new Error(`Rendered API ingresses use conflicting ${annotationName} annotations: ${values.join(', ')}`);
    }

    return values[0];
}

function readSubnetPrefixes(subnetId) {
    const subnet = runJson(
        'az',
        [
            'network',
            'vnet',
            'subnet',
            'show',
            '--ids',
            subnetId,
            '--query',
            '{id:id,addressPrefix:addressPrefix,addressPrefixes:addressPrefixes,delegations:delegations[].serviceName}',
            '--output',
            'json',
            '--only-show-errors'
        ],
        `Azure subnet ${subnetId}`
    );

    if (!subnet || typeof subnet !== 'object' || Array.isArray(subnet)) {
        throw new Error(`Azure subnet ${subnetId} returned an invalid object`);
    }

    if (typeof subnet.id !== 'string' || subnet.id.toLowerCase() !== subnetId.toLowerCase()) {
        throw new Error(`Azure returned a different subnet for association ${subnetId}`);
    }

    if (!Array.isArray(subnet.delegations) || !subnet.delegations.includes(TRAFFIC_CONTROLLER_DELEGATION)) {
        throw new Error(`Association subnet ${subnetId} is not delegated to ${TRAFFIC_CONTROLLER_DELEGATION}`);
    }

    const hasInvalidAddressPrefix =
        subnet.addressPrefix !== null && subnet.addressPrefix !== undefined && typeof subnet.addressPrefix !== 'string';

    if (hasInvalidAddressPrefix) {
        throw new Error(`Association subnet ${subnetId} returned an invalid addressPrefix`);
    }

    const hasInvalidAddressPrefixes =
        subnet.addressPrefixes !== null &&
        subnet.addressPrefixes !== undefined &&
        (!Array.isArray(subnet.addressPrefixes) ||
            !subnet.addressPrefixes.every(prefix => typeof prefix === 'string'));

    if (hasInvalidAddressPrefixes) {
        throw new Error(`Association subnet ${subnetId} returned invalid addressPrefixes`);
    }

    const addressPrefixes = subnet.addressPrefixes ?? [];
    const prefixes = [subnet.addressPrefix, ...addressPrefixes].filter(prefix => prefix !== null && prefix !== undefined);

    if (prefixes.length === 0) {
        throw new Error(`Association subnet ${subnetId} has no address prefixes`);
    }

    for (const prefix of prefixes) {
        const [address, prefixLength, extra] = typeof prefix === 'string' ? prefix.split('/') : [];
        const addressFamily = isIP(address ?? '');
        const maximumPrefixLength = addressFamily === 4 ? 32 : 128;
        const hasValidPrefixLength =
            /^[1-9][0-9]*$/.test(prefixLength ?? '') && Number(prefixLength) <= maximumPrefixLength;

        if (!addressFamily || extra !== undefined || !hasValidPrefixLength) {
            throw new Error(`Association subnet ${subnetId} has an invalid or unrestricted prefix: ${prefix}`);
        }
    }

    return prefixes;
}

function run(command, arguments_) {
    return execFileSync(command, arguments_, {
        encoding: 'utf8',
        timeout: 60_000
    });
}

function runJson(command, arguments_, description) {
    const output = run(command, arguments_);

    try {
        return JSON.parse(output);
    } catch (error) {
        throw new Error(`${description} returned invalid JSON`, { cause: error });
    }
}

function writeValuesFile(outputFile, values) {
    const temporaryFile = `${outputFile}.${randomUUID()}.tmp`;

    try {
        writeFileSync(temporaryFile, `${JSON.stringify(values, null, 2)}\n`, {
            encoding: 'utf8',
            flag: 'wx',
            mode: 0o600
        });
        renameSync(temporaryFile, outputFile);
    } finally {
        rmSync(temporaryFile, { force: true });
    }
}

const isMainModule = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;

if (isMainModule) {
    try {
        main();
    } catch (error) {
        console.error(`Forwarded-header discovery failed: ${error.message}`);
        process.exitCode = 1;
    }
}
