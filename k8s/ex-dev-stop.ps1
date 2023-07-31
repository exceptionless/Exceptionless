$ENV="dev"

kubectl scale deployment/ex-$ENV-app --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-api --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-close-inactive-sessions --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-daily-summary --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-notifications --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-usage --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-posts --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-user-descriptions --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-mail-message --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-stack-event-count --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-web-hooks --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-work-item --replicas=0 --namespace ex-$ENV

kubectl patch cronjob/ex-$ENV-jobs-cleanup-data -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-cleanup-orphaned-data -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-download-geoip-database -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-maintain-indexes -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-migration -p '{"spec":{"suspend": true}}' --namespace ex-$ENV

kubectl scale deployment/ex-$ENV-kb --replicas=0 --namespace ex-$ENV
kubectl scale statefulset/ex-$ENV-es-main --replicas=0 --namespace ex-$ENV
kubectl scale statefulset/ex-$ENV-redis-node --replicas=0 --namespace ex-$ENV
