# kubernetes helper tool
brew install kubectx

# azure cli
brew install azure-cli

# install helm
brew install helm
helm repo add stable https://kubernetes-charts.storage.googleapis.com/
helm repo add jetstack https://charts.jetstack.io
helm repo update

### setup

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
    --kubernetes-version 1.14.8 \
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

# install dashboard, using 2.0 rc5 that supports CRDs (elastic operator)
# https://github.com/kubernetes/dashboard/releases
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.0.0-rc5/aio/deploy/recommended.yaml

# create admin user to login to the dashboard
kubectl apply -f admin-service-account.yaml

# set the namespace
kubectl config set-context --current --namespace=ex-$ENV

# setup elasticsearch operator
# https://www.elastic.co/guide/en/cloud-on-k8s/current/k8s-quickstart.html
# https://github.com/elastic/cloud-on-k8s/releases
kubectl apply -f https://download.elastic.co/downloads/eck/1.0.1/all-in-one.yaml

# view ES operator logs
kubectl -n elastic-system logs -f statefulset.apps/elastic-operator

# create elasticsearch and kibana instances
kubectl apply -f ex-dev-elasticsearch.yaml
# check on deployment, wait for green
kubectl get elasticsearch
kubectl get es && k get pods -l common.k8s.elastic.co/type=elasticsearch

# get elastic password into env variable
ELASTIC_PASSWORD=$(kubectl get secret "ex-$ENV-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}')

# port forward elasticsearch
kubectl port-forward service/ex-$ENV-es-http 9200

# install nginx ingress
helm install nginx-ingress stable/nginx-ingress --namespace kube-system --values nginx-values.yaml

# wait for external ip to be assigned
kubectl get service -l app=nginx-ingress --namespace kube-system
IP="$(kubectl get service -l app=nginx-ingress --namespace kube-system -o=jsonpath='{.items[0].status.loadBalancer.ingress[0].ip}')"
PUBLICIPID=$(az network public-ip list --query "[?ipAddress!=null]|[?contains(ipAddress, '$IP')].[id]" --output tsv)
az network public-ip update --ids $PUBLICIPID --dns-name $CLUSTER

# install cert-manager
# https://github.com/jetstack/cert-manager/releases
kubectl apply --validate=false -f https://raw.githubusercontent.com/jetstack/cert-manager/v0.13.1/deploy/manifests/00-crds.yaml
kubectl create namespace cert-manager
kubectl apply -f cluster-issuer.yaml
helm install cert-manager jetstack/cert-manager --namespace cert-manager --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer

# TODO: update this file using the cluster name for the dns
kubectl apply -f certificates.yaml
kubectl describe certificate -n tls-secret

# install redis server
helm install ex-$ENV-redis stable/redis --values redis-values.yaml --namespace ex-$ENV

# install exceptionless app
APP_TAG="2.8.1502-pre"
API_TAG="6.0.3534-pre"
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

# create service principal for talking to k8s
ACCOUNT=`az account show -o json`
SUBSCRIPTION_ID=`echo $ACCOUNT | jq -r '.id'`
AZ_TENANT=`echo $ACCOUNT | jq -r '.tenantId'`
SERVICE_PRINCIPAL=`az ad sp create-for-rbac --role="Azure Kubernetes Service Cluster User Role" --name http://$CLUSTER-ci --scopes="/subscriptions/$SUBSCRIPTION_ID" -o json`
AZ_USERNAME=`echo $SERVICE_PRINCIPAL | jq -r '.appId'`
AZ_PASSWORD=`echo $SERVICE_PRINCIPAL | jq -r '.password'`
echo "AZ_USERNAME=$AZ_USERNAME AZ_PASSWORD=$AZ_PASSWORD AZ_TENANT=$AZ_TENANT | az login --service-principal --username \$AZ_USERNAME --password \$AZ_PASSWORD --tenant \$AZ_TENANT"

# delete the entire thing
az aks delete --resource-group $RESOURCE_GROUP --name $CLUSTER

# https://support.binarylane.com.au/support/solutions/articles/1000055889-how-to-benchmark-disk-i-o