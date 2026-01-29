import type {
    BucketAggregate,
    DateHistogramBucket,
    ExtendedStatsAggregate,
    IAggregate,
    IBucket,
    KeyedBucket,
    MultiBucketAggregate,
    ObjectValueAggregate,
    PercentileItem,
    PercentilesAggregate,
    SingleBucketAggregate,
    StatsAggregate,
    TermsAggregate,
    TopHitsAggregate,
    ValueAggregate
} from '../models';

type Aggregations = null | Record<string, IAggregate> | undefined;

export function average(aggregations: Aggregations, key: string): undefined | ValueAggregate {
    return tryGet<ValueAggregate>(aggregations, key);
}

export function cardinality(aggregations: Aggregations, key: string): undefined | ValueAggregate {
    return tryGet<ValueAggregate>(aggregations, key);
}

export function dateHistogram(aggregations: Aggregations, key: string): MultiBucketAggregate<DateHistogramBucket> | undefined {
    return getMultiBucketAggregate<DateHistogramBucket>(aggregations, key);
}

export function extendedStats(aggregations: Aggregations, key: string): ExtendedStatsAggregate | undefined {
    return tryGet<ExtendedStatsAggregate>(aggregations, key);
}

export function geoHash(aggregations: Aggregations, key: string): MultiBucketAggregate<KeyedBucket<string>> | undefined {
    return getMultiKeyedBucketAggregate<string>(aggregations, key);
}

export function getPercentile(agg: PercentilesAggregate, percentile: number): PercentileItem | undefined {
    return agg.items.find((i) => i.percentile === percentile); // Checked
}

export function max<T = number>(aggregations: Aggregations, key: string): undefined | ValueAggregate<T> {
    return tryGet<ValueAggregate<T>>(aggregations, key);
}

export function metric(aggregations: Aggregations, key: string): ObjectValueAggregate | undefined {
    const valueMetric = tryGet<ValueAggregate>(aggregations, key);
    if (valueMetric) {
        return <ObjectValueAggregate>{
            data: valueMetric.data,
            value: valueMetric.value
        };
    }

    return tryGet<ObjectValueAggregate>(aggregations, key);
}

export function min<T = number>(aggregations: Aggregations, key: string): undefined | ValueAggregate<T> {
    return tryGet<ValueAggregate<T>>(aggregations, key);
}

export function missing(aggregations: Aggregations, key: string): SingleBucketAggregate | undefined {
    return tryGet<SingleBucketAggregate>(aggregations, key);
}

export function percentiles(aggregations: Aggregations, key: string): PercentilesAggregate | undefined {
    return tryGet<PercentilesAggregate>(aggregations, key);
}

export function stats(aggregations: Aggregations, key: string): StatsAggregate | undefined {
    return tryGet<StatsAggregate>(aggregations, key);
}

export function sum(aggregations: Aggregations, key: string): undefined | ValueAggregate {
    return tryGet<ValueAggregate>(aggregations, key);
}

export function terms<TKey = string>(aggregations: Aggregations, key: string): TermsAggregate<TKey> | undefined {
    const bucket = tryGet<BucketAggregate>(aggregations, key);
    if (!bucket) {
        return;
    }

    return <TermsAggregate<TKey>>{
        buckets: bucket.items ? getKeyedBuckets<TKey>(bucket.items) : [],
        data: bucket.data
    };
}

export function topHits<T = unknown>(aggregations: Aggregations): TopHitsAggregate<T> | undefined {
    return tryGet<TopHitsAggregate<T>>(aggregations, 'tophits');
}

function getKeyedBuckets<TKey>(items: IBucket[]): KeyedBucket<TKey>[] {
    return items
        .filter((bucket): bucket is KeyedBucket<TKey> => 'key' in bucket)
        .map((bucket) => ({
            aggregations: bucket.aggregations,
            key: bucket.key, // NOTE: May have to convert to proper type
            key_as_string: bucket.key_as_string,
            total: bucket.total
        }));
}

function getMultiBucketAggregate<TBucket extends IBucket>(aggregations: Aggregations, key: string): MultiBucketAggregate<TBucket> | undefined {
    const bucket = tryGet<BucketAggregate>(aggregations, key);
    if (!bucket) {
        return;
    }

    return <MultiBucketAggregate<TBucket>>{
        buckets: bucket.items ?? [],
        data: bucket.data
    };
}

function getMultiKeyedBucketAggregate<TKey>(aggregations: Aggregations, key: string): MultiBucketAggregate<KeyedBucket<TKey>> | undefined {
    const bucket = tryGet<BucketAggregate>(aggregations, key);
    if (!bucket) {
        return;
    }

    return <MultiBucketAggregate<KeyedBucket<TKey>>>{
        buckets: bucket.items ? getKeyedBuckets<TKey>(bucket.items) : [],
        data: bucket.data
    };
}

function tryGet<TAggregate extends IAggregate>(aggregations: Aggregations, key: string): TAggregate | undefined {
    return aggregations?.[key] as TAggregate;
}
