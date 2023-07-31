$ENV="dev"

kubectl scale statefulset/ex-$ENV-es-main --replicas=1 --namespace ex-$ENV
kubectl scale statefulset/ex-$ENV-redis-node --replicas=1 --namespace ex-$ENV

kubectl wait --for=condition=ready --timeout=300s pod ex-$ENV-es-main-0 --namespace ex-$ENV

kubectl scale deployment/ex-$ENV-kb --replicas=1 --namespace ex-$ENV

kubectl scale deployment/ex-$ENV-app --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-api --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-close-inactive-sessions --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-daily-summary --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-notifications --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-usage --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-posts --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-user-descriptions --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-mail-message --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-stack-event-count --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-web-hooks --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-work-item --replicas=1 --namespace ex-$ENV

kubectl patch cronjob/ex-$ENV-jobs-cleanup-data -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-cleanup-orphaned-data -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-download-geoip-database -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-maintain-indexes -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-migration -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
