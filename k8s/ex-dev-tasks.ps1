# get elasticsearch password
$ELASTIC_PASSWORD=$(kubectl get secret --namespace ex-dev "ex-dev-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}')

# connect to kibana
kubectl port-forward --namespace ex-dev service/ex-dev-kb-http 5601
open "http://kibana-ex-dev.localtest.me:5601"

# port forward elasticsearch
$ELASTIC_JOB = kubectl port-forward --namespace ex-dev service/ex-dev-es-http 9200 &
Remove-Job $ELASTIC_JOB

# connect to redis
$REDIS_PASSWORD=$(kubectl get secret --namespace ex-dev ex-dev-redis -o go-template='{{index .data \"redis-password\" | base64decode }}')
kubectl port-forward --namespace ex-dev service/ex-dev-redis 6379
redis-cli -a $REDIS_PASSWORD

# open kubernetes dashboard
$DASHBOARD_PASSWORD=$(kubectl get secret --namespace kubernetes-dashboard admin-user-token-w8jg7 -o go-template='{{.data.token | base64decode }}')
kubectl proxy
open "http://localhost:8001/api/v1/namespaces/kubernetes-dashboard/services/https:kubernetes-dashboard:/proxy/"

# open kubecost
kubectl port-forward --namespace kubecost deployment/kubecost-cost-analyzer 9090

# run a shell inside kubernetes cluster
kubectl run -it --rm aks-ssh --image=ubuntu
# ssh to k8s node https://docs.microsoft.com/en-us/azure/aks/ssh

# view ES operator logs
kubectl -n elastic-system logs -f statefulset.apps/elastic-operator

# get elasticsearch and its pods
kubectl get es && kubectl get pods -l common.k8s.elastic.co/type=elasticsearch

# manually run a job
kubectl run --namespace ex-dev ex-dev-client --rm --tty -i --restart='Never' `
    --env ELASTIC_PASSWORD=$ELASTIC_PASSWORD `
    --image exceptionless/api-ci:$API_TAG -- bash

# upgrade nginx ingress and cert-manager
# look in ex-prod-tasks.ps1

# upgrade elasticsearch
kubectl apply -f ex-dev-elasticsearch.yaml

# upgrade redis
helm repo update
helm upgrade ex-dev-redis bitnami/redis --values ex-dev-redis-values.yaml --namespace ex-dev

# upgrade exceptionless app to a new docker image tag
$VERSION="8.0.0"
helm upgrade --set "version=$VERSION" --reuse-values ex-dev .\exceptionless

# upgrade exceptionless app to set a new env variable
helm upgrade `
    --set "elasticsearch.connectionString=$ELASTIC_CONNECTIONSTRING" `
    --set "email.connectionString=$EMAIL_CONNECTIONSTRING" `
    --set "queue.connectionString=$QUEUE_CONNECTIONSTRING" `
    --set "redis.connectionString=$REDIS_CONNECTIONSTRING" `
    --set "storage.connectionString=$STORAGE_CONNECTIONSTRING" `
    --set "config.EX_StripeApiKey=$EX_StripeApiKey" `
    --set "config.EX_StripePublishableApiKey=$EX_StripePublishableApiKey" `
    --set "config.EX_StripeWebHookSigningSecret=$EX_StripeWebHookSigningSecret" `
    --reuse-values ex-dev --namespace ex-dev .\exceptionless

helm upgrade --set "redis.connectionString=$REDIS_CONNECTIONSTRING" --reuse-values ex-dev --namespace ex-dev .\exceptionless
helm upgrade --reuse-values ex-dev --namespace ex-dev .\exceptionless --dry-run | code-insiders -
$VERSION="8.0.0"
helm upgrade ex-dev .\exceptionless --namespace ex-dev --values ex-dev-values.yaml `
    --set "version=$VERSION" `
    --set "elasticsearch.connectionString=$ELASTIC_CONNECTIONSTRING" `
    --set "email.connectionString=$EMAIL_CONNECTIONSTRING" `
    --set "queue.connectionString=$QUEUE_CONNECTIONSTRING" `
    --set "redis.connectionString=$REDIS_CONNECTIONSTRING" `
    --set "storage.connectionString=$STORAGE_CONNECTIONSTRING" `
    --set "config.EX_StripeApiKey=$EX_StripeApiKey" `
    --set "config.EX_StripePublishableApiKey=$EX_StripePublishableApiKey" `
    --set "config.EX_StripeWebHookSigningSecret=$EX_StripeWebHookSigningSecret" --dry-run | code-insiders -

# stop the entire app
kubectl scale deployment/ex-dev-app --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-api --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-close-inactive-sessions --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-daily-summary --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-notifications --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-posts --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-user-descriptions --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-mail-message --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-stack-event-count --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-web-hooks --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-work-item --replicas=0 --namespace ex-dev

kubectl patch cronjob/ex-dev-jobs-cleanup-data -p '{\"spec\":{\"suspend\": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-download-geoip-database -p '{\"spec\":{\"suspend\": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-maintain-indexes -p '{\"spec\":{\"suspend\": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-migration -p '{\"spec\":{\"suspend\": true}}' --namespace ex-dev

# resume the app
kubectl scale deployment/ex-dev-app --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-api --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-close-inactive-sessions --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-daily-summary --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-notifications --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-posts --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-user-descriptions --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-mail-message --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-stack-event-count --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-web-hooks --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-work-item --replicas=1 --namespace ex-dev

kubectl patch cronjob/ex-dev-jobs-cleanup-data -p '{\"spec\":{\"suspend\": false}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-download-geoip-database -p '{\"spec\":{\"suspend\": false}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-maintain-indexes -p '{\"spec\":{\"suspend\": false}}' --namespace ex-dev
