# TODO: Set AZURE_ACCOUNT_KEY and REDIS_PASSWORD environment variables

RESOURCE_GROUP=exceptionless-v6
CLUSTER=ex-k8s-v6
VNET=ex-net-v6
ENV=dev

# it's important to have a decent sized network (reserve a /16 for each cluster).
az network vnet create -g $RESOURCE_GROUP -n $VNET --subnet-name $CLUSTER --address-prefixes 10.60.0.0/16 --subnet-prefixes 10.60.0.0/18 --location eastus
SUBNET_ID="$(az network vnet subnet list --resource-group $RESOURCE_GROUP --vnet-name $VNET --query '[0].id' --output tsv)"

az aks create \
    --resource-group $RESOURCE_GROUP \
    --name $CLUSTER \
    --kubernetes-version 1.14.7 \
    --node-count 3 \
    --node-vm-size Standard_D8s_v3 \
    --max-pods 50 \
    --network-plugin azure \
    --vnet-subnet-id $SUBNET_ID \
    --enable-addons monitoring \
    --admin-username exuser \
    --ssh-key-value ~/.ssh/exceptionless.pub \
    --location eastus \
    --docker-bridge-address 172.17.0.1/16 \
    --dns-service-ip 10.60.192.10 \
    --service-cidr 10.60.192.0/18

az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER --overwrite-existing

# install dashboard, using 2.0 beta that supports CRDs (elastic operator)
# https://github.com/kubernetes/dashboard/releases
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.0.0-beta6/aio/deploy/recommended.yaml

# create admin user to login to the dashboard
kubectl apply -f admin-service-account.yaml

# get admin user token
kubectl -n kubernetes-dashboard describe secret $(kubectl -n kubernetes-dashboard get secret | grep admin-user | awk '{print $1}')

# set the namespace
kubectl config set-context --current --namespace=ex-$ENV

# open dashboard
kubectl proxy
# URL: http://localhost:8001/api/v1/namespaces/kubernetes-dashboard/services/https:kubernetes-dashboard:/proxy/#/login

# setup elasticsearch operator
# https://www.elastic.co/guide/en/cloud-on-k8s/current/k8s-quickstart.html
# https://github.com/elastic/cloud-on-k8s/releases
kubectl apply -f https://download.elastic.co/downloads/eck/1.0.0-beta1/all-in-one.yaml

# view ES operator logs
kubectl -n elastic-system logs -f statefulset.apps/elastic-operator

# create elasticsearch and kibana instances
kubectl apply -f ex-dev-elasticsearch.yaml
# check on deployment, wait for green
kubectl get elasticsearch

k get es,kb,apm,sts,pod
k get pods -l common.k8s.elastic.co/type=elasticsearch

# get elastic password into env variable
ELASTIC_PASSWORD=$(kubectl get secret "ex-$ENV-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}')
# port forward elasticsearch in background task
kubectl port-forward service/ex-$ENV-es-http 9200 &
# connect to ES
curl -u elastic:$ELASTIC_PASSWORD http://localhost:9200/

# view nodes with version
curl -u elastic:$ELASTIC_PASSWORD http://localhost:9200/_cat/nodes\?v\&h\=id,ip,port,v,m

# port forward kibana
kubectl port-forward service/ex-$ENV-kb-http 5601

# port forward elasticsearch
kubectl port-forward service/ex-$ENV-es-http 9200

# install helm
brew install helm
helm repo add stable https://kubernetes-charts.storage.googleapis.com

# install nginx ingress
helm install nginx-ingress stable/nginx-ingress --namespace kube-system --values nginx-values.yaml

# upgrade nginx ingress to latest
helm upgrade --reset-values --namespace kube-system -f nginx-values.yaml --dry-run nginx-ingress stable/nginx-ingress

# wait for external ip to be assigned
kubectl get service -l app=nginx-ingress --namespace kube-system
IP="$(kubectl get service -l app=nginx-ingress --namespace kube-system -o=jsonpath='{.items[0].status.loadBalancer.ingress[0].ip}')"
PUBLICIPID=$(az network public-ip list --query "[?ipAddress!=null]|[?contains(ipAddress, '$IP')].[id]" --output tsv)
az network public-ip update --ids $PUBLICIPID --dns-name $CLUSTER

# install cert-manager
kubectl apply --validate=false -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.11/deploy/manifests/00-crds.yaml
kubectl create namespace cert-manager
helm repo add jetstack https://charts.jetstack.io
helm repo update
kubectl apply -f cluster-issuer.yaml
helm install cert-manager jetstack/cert-manager --namespace kube-system --set ingressShim.defaultIssuerName=letsencrypt-$ENV --set ingressShim.defaultIssuerKind=ClusterIssuer

# TODO: update this file using the cluster name for the dns
kubectl apply -f certificates.yaml
kubectl describe certificate -n tls-secret

# install redis server
helm install ex-$ENV-redis stable/redis --values redis-values.yaml --namespace ex-$ENV

# get redis and elastic passwords
export REDIS_PASSWORD=$(kubectl get secret --namespace ex-$ENV ex-$ENV-redis -o jsonpath="{.data.redis-password}" | base64 --decode)
export ELASTIC_PASSWORD=$(kubectl get secret --namespace ex-$ENV ex-$ENV-es-elastic-user -o go-template='{{.data.elastic | base64decode }}')

# exec into a pod with redis and elastic password
kubectl run --namespace ex-$ENV ex-$ENV-client --rm --tty -i --restart='Never' \
    --env REDIS_PASSWORD=$REDIS_PASSWORD \
    --env ELASTIC_PASSWORD=$ELASTIC_PASSWORD \
    --image docker.io/bitnami/redis:5.0.6-debian-9-r1 -- bash

# run migration job
kubectl run --namespace ex-$ENV ex-$ENV-client --rm --tty -i --restart='Never' \
    --env ELASTIC_PASSWORD=$ELASTIC_PASSWORD \
    --image exceptionless/api-ci:$API_TAG -- bash

# commands to check services
# redis-cli -h redis-master -a $REDIS_PASSWORD
# curl -u elastic:$ELASTIC_PASSWORD http://ex-$ENV-es-http:9200/

# install exceptionless app
APP_TAG="2.8.1502-pre"
API_TAG="6.0.3534-pre"
ELASTIC_CONNECTIONSTRING=
EMAIL_CONNECTIONSTRING=
QUEUE_CONNECTIONSTRING=
REDIS_CONNECTIONSTRING=
STORAGE_CONNECTIONSTRING=
STATSD_TOKEN=
STATSD_USER=
EX_ApplicationInsightsKey=
EX_ConnectionStrings__OAuth=
EX_ExceptionlessApiKey=
EX_GoogleGeocodingApiKey=
EX_GoogleTagManagerId=
EX_StripeApiKey=
EX_StripePublishableApiKey=
EX_StripeWebHookSigningSecret=
EX_MaxMindGeoIpKey=

helm install ex-$ENV ./exceptionless --namespace ex-$ENV --values ex-$ENV-values.yaml \
    --set "app.image.tag=$APP_TAG" \
    --set "api.image.tag=$API_TAG" \
    --set "jobs.image.tag=$API_TAG" \
    --set "elasticsearch.connectionString=$ELASTIC_CONNECTIONSTRING" \
    --set "email.connectionString=$EMAIL_CONNECTIONSTRING" \
    --set "queue.connectionString=$QUEUE_CONNECTIONSTRING" \
    --set "redis.connectionString=$REDIS_CONNECTIONSTRING" \
    --set "storage.connectionString=$STORAGE_CONNECTIONSTRING" \
    --set "statsd.token=$STATSD_TOKEN" \
    --set "statsd.user=$STATSD_USER" \
    --set "config.EX_ApplicationInsightsKey=$EX_ApplicationInsightsKey" \
    --set "config.EX_ConnectionStrings__OAuth=$EX_ConnectionStrings__OAuth" \
    --set "config.EX_ExceptionlessApiKey=$EX_ExceptionlessApiKey" \
    --set "config.EX_GoogleGeocodingApiKey=$EX_GoogleGeocodingApiKey" \
    --set "config.EX_GoogleTagManagerId=$EX_GoogleTagManagerId" \
    --set "config.EX_StripeApiKey=$EX_StripeApiKey" \
    --set "config.EX_StripePublishableApiKey=$EX_StripePublishableApiKey" \
    --set "config.EX_MaxMindGeoIpKey=$EX_MaxMindGeoIpKey" \
    --set "config.EX_StripeWebHookSigningSecret=$EX_StripeWebHookSigningSecret"

# upgrade exceptionless app to a new docker image tag
helm upgrade --set "api.image.tag=$API_TAG" --set "jobs.image.tag=$API_TAG" --reuse-values ex-$ENV ./exceptionless

# create service principal for talking to k8s
ACCOUNT=`az account show -o json`
SUBSCRIPTION_ID=`echo $ACCOUNT | jq -r '.id'`
AZ_TENANT=`echo $ACCOUNT | jq -r '.tenantId'`
SERVICE_PRINCIPAL=`az ad sp create-for-rbac --role="Azure Kubernetes Service Cluster User Role" --name http://$CLUSTER-ci --scopes="/subscriptions/$SUBSCRIPTION_ID" -o json`
AZ_USERNAME=`echo $SERVICE_PRINCIPAL | jq -r '.appId'`
AZ_PASSWORD=`echo $SERVICE_PRINCIPAL | jq -r '.password'`
echo "AZ_USERNAME=$AZ_USERNAME AZ_PASSWORD=$AZ_PASSWORD AZ_TENANT=$AZ_TENANT | az login --service-principal --username \$AZ_USERNAME --password \$AZ_PASSWORD --tenant \$AZ_TENANT"

# run a shell
kubectl run -it --rm aks-ssh --image=ubuntu
# ssh to k8s node https://docs.microsoft.com/en-us/azure/aks/ssh

# stop the entire app
kubectl scale deployment/ex-$ENV-api --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-collector --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-close-inactive-sessions --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-daily-summary --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-notifications --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-posts --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-user-descriptions --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-mail-message --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-retention-limits --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-stack-event-count --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-web-hooks --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-work-item --replicas=0 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-statsd --replicas=0 --namespace ex-$ENV

kubectl patch cronjob/ex-$ENV-jobs-cleanup-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-download-geoip-database -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-event-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-maintain-indexes -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-organization-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-stack-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-$ENV

# resume the app
kubectl scale deployment/ex-$ENV-api --replicas=5 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-collector --replicas=12 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-close-inactive-sessions --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-daily-summary --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-notifications --replicas=2 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-posts --replicas=6 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-event-user-descriptions --replicas=2 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-mail-message --replicas=2 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-retention-limits --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-stack-event-count --replicas=1 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-web-hooks --replicas=4 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-jobs-work-item --replicas=5 --namespace ex-$ENV
kubectl scale deployment/ex-$ENV-statsd --replicas=1 --namespace ex-$ENV

kubectl patch cronjob/ex-$ENV-jobs-cleanup-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-download-geoip-database -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-event-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-maintain-indexes -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-organization-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-$ENV
kubectl patch cronjob/ex-$ENV-jobs-stack-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-$ENV

# view pod log tail
kubectl logs -f ex-$ENV-jobs-event-posts-6c7b78d745-xd5ln

# install helper tools for using kubernetes CLI
brew install kubectx

# install tool to view pod logs
brew tap johanhaleby/kubetail
brew install kubetail

az aks delete --resource-group $RESOURCE_GROUP --name $CLUSTER

# install exceptionless slack
helm repo add banzaicloud-stable https://kubernetes-charts.banzaicloud.com
helm install ex-$ENV-slack banzaicloud-stable/slackin --namespace ex-$ENV --values ex-slack-values.yaml --set "slackApiToken=$SLACK_API_TOKEN" --set "googleCaptchaSecret=$CAPTCHA_SECRET" --set "googleCaptchaSiteKey=$CAPTCHA_KEY"

# https://support.binarylane.com.au/support/solutions/articles/1000055889-how-to-benchmark-disk-i-o`