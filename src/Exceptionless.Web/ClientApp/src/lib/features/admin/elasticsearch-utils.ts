export function healthBadgeClass(status: null | number | string | undefined): string {
    const s = typeof status === 'string' ? status.toLowerCase() : status;

    if (s === 0 || s === 'green') {
        return 'text-muted-foreground border-muted-foreground/30';
    }

    if (s === 1 || s === 'yellow') {
        return 'border-amber-500 text-amber-600 dark:text-amber-400';
    }

    if (s === 2 || s === 'red') {
        return 'border-destructive/50 text-destructive';
    }

    return 'text-muted-foreground border-muted-foreground/30';
}

export function healthColor(status: null | number | string | undefined): string {
    const s = typeof status === 'string' ? status.toLowerCase() : status;

    if (s === 0 || s === 'green') {
        return 'text-green-600';
    }

    if (s === 1 || s === 'yellow') {
        return 'text-amber-500';
    }

    if (s === 2 || s === 'red') {
        return 'text-destructive';
    }

    return 'text-muted-foreground';
}

export function healthLabel(status: null | number | string | undefined): string {
    if (typeof status === 'string') {
        return status.charAt(0).toUpperCase() + status.slice(1).toLowerCase();
    }
    switch (status) {
        case 0:
            return 'Green';
        case 1:
            return 'Yellow';
        case 2:
            return 'Red';
        default:
            return 'Unknown';
    }
}

export function healthVariant(status: null | number | string | undefined): 'default' | 'destructive' | 'outline' | 'secondary' {
    const s = typeof status === 'string' ? status.toLowerCase() : status;

    if (s === 2 || s === 'red') {
        return 'destructive';
    }

    return 'outline';
}

export function snapshotBadgeClass(status: string | undefined): string {
    switch (status?.toUpperCase()) {
        case 'IN_PROGRESS':
            return 'border-blue-500 text-blue-600 dark:text-blue-400';
        case 'PARTIAL':
            return 'border-amber-500 text-amber-600 dark:text-amber-400';
        case 'SUCCESS':
            return 'text-muted-foreground border-muted-foreground/30';
        default:
            return 'text-muted-foreground border-muted-foreground/30';
    }
}

export function snapshotVariant(status: string | undefined): 'default' | 'destructive' | 'outline' | 'secondary' {
    switch (status?.toUpperCase()) {
        case 'FAILED':
            return 'destructive';
        case 'IN_PROGRESS':
            return 'secondary';
        default:
            return 'outline';
    }
}
