# get elasticsearch password
$ELASTIC_PASSWORD=$(kubectl get secret --namespace ex-prod "ex-prod-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}')

# connect to kibana
open "http://kibana-ex-prod.localtest.me:5660" && kubectl port-forward --namespace ex-prod service/ex-prod-kb-http 5660:5601

# port forward elasticsearch
$ELASTIC_JOB = kubectl port-forward --namespace ex-prod service/ex-prod-es-http 9260:9200 &
Remove-Job $ELASTIC_JOB
curl -k https://elastic:$ELASTIC_PASSWORD@localhost:9260/_cluster/health?pretty

# port forward monitoring elasticsearch
$ELASTIC_MONITOR_PASSWORD=$(kubectl get secret --namespace elastic-system "elastic-monitor-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}')
$ELASTIC_JOB = kubectl port-forward --namespace elastic-system service/elastic-monitor-es-http 9280:9200 &
Remove-Job $ELASTIC_JOB

curl -k https://elastic:$ELASTIC_MONITOR_PASSWORD@localhost:9280/_cluster/health?pretty
curl -k https://elastic:$ELASTIC_MONITOR_PASSWORD@localhost:9280/_cat/allocation?v
curl -k https://elastic:$ELASTIC_MONITOR_PASSWORD@localhost:9280/_cluster/allocation/explain?pretty
curl -k "https://elastic:$ELASTIC_MONITOR_PASSWORD@localhost:9280/_cat/indices/*traces*?v=true&s=index"
curl -X PUT -H "Content-Type: application/json" -g -k -d '{ "transient": { "action.destructive_requires_name": false } }' https://elastic:$ELASTIC_MONITOR_PASSWORD@localhost:9280/_cluster/settings
curl -k -X DELETE "https://elastic:$ELASTIC_MONITOR_PASSWORD@localhost:9280/.ds-traces-apm-default-2022.09.01-000108"

# connect to redis OR use k9s to shell into a redis pod
$REDIS_PASSWORD=$(kubectl get secret --namespace ex-prod ex-prod-redis -o go-template='{{index .data "redis-password" | base64decode }}')
kubectl exec --stdin --tty ex-prod-redis-node-0 -- /bin/bash -c "redis-cli -a $REDIS_PASSWORD"

# open kubernetes dashboard
$DASHBOARD_PASSWORD=$(kubectl get secret --namespace kubernetes-dashboard admin-user-token-w8jg7 -o go-template='{{.data.token | base64decode }}')
kubectl proxy
open "http://localhost:8001/api/v1/namespaces/kubernetes-dashboard/services/https:kubernetes-dashboard:/proxy/"

# open kubecost
kubectl port-forward --namespace kubecost deployment/kubecost-cost-analyzer 9090

# open goldilocks
kubectl -n goldilocks port-forward svc/goldilocks-dashboard 8080:80

# run a shell inside kubernetes cluster
kubectl run -it --rm aks-ssh --image=ubuntu
# ssh to k8s node https://docs.microsoft.com/en-us/azure/aks/ssh

# view ES operator logs
kubectl -n elastic-system logs -f statefulset.apps/elastic-operator

# get elasticsearch and its pods
kubectl get es && kubectl get pods --namespace ex-prod -l common.k8s.elastic.co/type=elasticsearch

# manually run a job
kubectl run --namespace ex-prod ex-prod-client --rm --tty -i --restart='Never' `
    --env ELASTIC_PASSWORD=$ELASTIC_PASSWORD `
    --image exceptionless/api-ci:$API_TAG -- bash

# upgrade nginx ingress to latest
# https://github.com/kubernetes/ingress-nginx/releases
helm repo update
helm upgrade --reset-values --namespace ingress-nginx -f nginx-values.yaml ingress-nginx ingress-nginx/ingress-nginx --dry-run

# upgrade cert-manager
# https://github.com/jetstack/cert-manager/releases
helm repo update
helm upgrade cert-manager jetstack/cert-manager --namespace cert-manager --reset-values --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer --set installCRDs=true --dry-run

# upgrade kube-state-metrics
helm upgrade --namespace elastic-system kube-state-metrics prometheus-community/kube-state-metrics --reset-values

# upgrade dashboard
# https://github.com/kubernetes/dashboard/releases
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml

# upgrade kubecost
helm repo update
helm upgrade kubecost kubecost/cost-analyzer --namespace kubecost --reset-values --set kubecostToken="ZXJpY0Bjb2Rlc21pdGh0b29scy5jb20=xm343yadf98" --dry-run

# upgrade goldilocks
helm repo update
helm upgrade goldilocks fairwinds-stable/goldilocks --namespace goldilocks --reset-values --dry-run
helm upgrade vpa fairwinds-stable/vpa --namespace vpa -f vpa-values.yaml --reset-values --dry-run

# upgrade elasticsearch operator
# https://www.elastic.co/guide/en/cloud-on-k8s/current/k8s-quickstart.html
# https://github.com/elastic/cloud-on-k8s/releases
kubectl replace -f https://download.elastic.co/downloads/eck/2.13.0/crds.yaml
kubectl create -f https://download.elastic.co/downloads/eck/2.13.0/crds.yaml
kubectl apply -f https://download.elastic.co/downloads/eck/2.13.0/operator.yaml

# upgrade elasticsearch
kubectl apply --namespace ex-prod -f ex-prod-elasticsearch.yaml

# upgrade elastic monitor
kubectl apply --namespace elastic-system -f elastic-monitor.yaml

# upgrade exceptionless app to a new docker image tag
$VERSION="8.0.0"
helm upgrade --set "version=$VERSION" --reuse-values ex-prod --namespace ex-prod .\exceptionless
helm upgrade --reuse-values ex-prod --namespace ex-prod .\exceptionless
# see what an upgrade will do
helm diff upgrade --reuse-values ex-prod --namespace ex-prod .\exceptionless

# upgrade exceptionless app to set a new env variable
helm upgrade `
    --set "elasticsearch.connectionString=$ELASTIC_CONNECTIONSTRING" `
    --set "email.connectionString=$EMAIL_CONNECTIONSTRING" `
    --set "queue.connectionString=$QUEUE_CONNECTIONSTRING" `
    --set "redis.connectionString=$REDIS_CONNECTIONSTRING" `
    --set "storage.connectionString=$STORAGE_CONNECTIONSTRING" `
    --set "config.EX_ApplicationInsightsKey=$EX_ApplicationInsightsKey" `
    --set "config.EX_ConnectionStrings__OAuth=$EX_ConnectionStrings__OAuth" `
    --set "config.EX_ExceptionlessApiKey=$EX_ExceptionlessApiKey" `
    --set "config.EX_GoogleGeocodingApiKey=$EX_GoogleGeocodingApiKey" `
    --set "config.EX_GoogleTagManagerId=$EX_GoogleTagManagerId" `
    --set "config.EX_StripeApiKey=$EX_StripeApiKey" `
    --set "config.EX_StripePublishableApiKey=$EX_StripePublishableApiKey" `
    --set "config.EX_MaxMindGeoIpKey=$EX_MaxMindGeoIpKey" `
    --set "config.EX_StripeWebHookSigningSecret=$EX_StripeWebHookSigningSecret" `
    --reuse-values ex-prod --namespace ex-prod .\exceptionless

helm upgrade --set "redis.connectionString=$REDIS_CONNECTIONSTRING" --reuse-values ex-prod --namespace ex-prod .\exceptionless

# stop the entire app

kubectl scale deployment/ex-prod-app --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-api --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-close-inactive-sessions --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-daily-summary --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-notifications --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-usage --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-posts --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-user-descriptions --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-mail-message --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-stack-event-count --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-web-hooks --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-work-item --replicas=0 --namespace ex-prod

kubectl patch cronjob/ex-prod-jobs-cleanup-data -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-cleanup-orphaned-data -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-download-geoip-database -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-maintain-indexes -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-migration -p '{"spec":{"suspend": true}}' --namespace ex-prod

# resume the app

kubectl scale deployment/ex-prod-app --replicas=5 --namespace ex-prod
kubectl scale deployment/ex-prod-api --replicas=5 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-close-inactive-sessions --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-daily-summary --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-notifications --replicas=2 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-usage --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-posts --replicas=6 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-user-descriptions --replicas=2 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-mail-message --replicas=2 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-stack-event-count --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-web-hooks --replicas=4 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-work-item --replicas=5 --namespace ex-prod

kubectl patch cronjob/ex-prod-jobs-cleanup-data -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-cleanup-orphaned-data -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-download-geoip-database -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-maintain-indexes -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-migration -p '{"spec":{"suspend": false}}' --namespace ex-prod

# get memory dump for running pod (https://pgroene.wordpress.com/2021/02/17/memory-dump-net-core-linux-container-aks/)

kubectl get pods

kubectl exec -it ex-prod-jobs-event-posts-6f69556577-fc52v --namespace ex-prod -- /bin/bash

apt update
apt install wget
wget -O sdk_install.sh https://dot.net/v1/dotnet-install.sh
chmod 777 sdk_install.sh
./sdk_install.sh -c 6.0
cd /root/.dotnet
./dotnet tool install --global dotnet-gcdump
./dotnet tool install --global dotnet-stack
cd tools
./dotnet-gcdump ps
./dotnet-gcdump collect -p 1
./dotnet-stack ps
./dotnet-stack report -p 1
exit

kubectl cp ex-prod-jobs-event-posts-6f69556577-fc52v:/root/.dotnet/tools/20220908_191124_1.gcdump ./20220908_191124_1.gcdump --retries=50
