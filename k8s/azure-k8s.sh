# TODO: Set AZURE_ACCOUNT_KEY and REDIS_PASSWORD environment variables

RESOURCE_GROUP=exceptionless-v4
CLUSTER=ex-prod-k8s
VNET=ex-prod-net

# it's important to have a decent sized network (reserve a /16 for each cluster).
az network vnet create -g $RESOURCE_GROUP -n $VNET --subnet-name $CLUSTER --address-prefixes 10.10.0.0/16 --subnet-prefixes 10.10.0.0/18 --location eastus
SUBNET_ID="$(az network vnet subnet list --resource-group $RESOURCE_GROUP --vnet-name $VNET --query '[0].id' --output tsv)"

az aks create \
    --resource-group $RESOURCE_GROUP \
    --name $CLUSTER \
    --kubernetes-version 1.11.5 \
    --node-count 3 \
    --node-vm-size Standard_D16s_v3 \
    --network-plugin azure \
    --vnet-subnet-id $SUBNET_ID \
    --enable-addons monitoring \
    --admin-username exuser \
    --ssh-key-value ~/.ssh/exceptionless.pub \
    --location eastus \
    --docker-bridge-address 172.17.0.1/16 \
    --dns-service-ip 10.10.192.10 \
    --service-cidr 10.10.192.0/18

az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER --overwrite-existing

# create dashboard service account
kubectl create clusterrolebinding kubernetes-dashboard --clusterrole=cluster-admin --serviceaccount=kube-system:kubernetes-dashboard

# start dashboard
az aks browse --resource-group $RESOURCE_GROUP --name $CLUSTER

# or
kubectl proxy
# http://localhost:8001/api/v1/namespaces/kube-system/services/kubernetes-dashboard/proxy/#!/cluster?namespace=default

# install helm
brew install kubernetes-helm
kubectl apply -f helm-rbac.yaml
helm init --service-account tiller

# install nginx ingress
helm install stable/nginx-ingress --namespace kube-system --name nginx-ingress --set controller.replicaCount=2

helm upgrade --set "controller.stats.enabled=true" --set "controller.metrics.enabled=true" --reuse-values nginx-ingress nginx-ingress stable/nginx-ingress

# wait for external ip to be assigned
kubectl get service -l app=nginx-ingress --namespace kube-system
IP="$(kubectl get service -l app=nginx-ingress --namespace kube-system -o=jsonpath='{.items[0].status.loadBalancer.ingress[0].ip}')"
PUBLICIPID=$(az network public-ip list --query "[?ipAddress!=null]|[?contains(ipAddress, '$IP')].[id]" --output tsv)
az network public-ip update --ids $PUBLICIPID --dns-name $CLUSTER

# install cert-manager
helm install stable/cert-manager \
    --namespace kube-system \
    --name cert-manager \
    --set ingressShim.defaultIssuerName=letsencrypt-prod \
    --set ingressShim.defaultIssuerKind=ClusterIssuer

kubectl apply -f cluster-issuer.yaml

# TODO: update this file using the cluster name for the dns
kubectl apply -f certificates.yaml

# install redis server
helm install stable/redis --values redis-values.yaml --name redis
export REDIS_PASSWORD=$(kubectl get secret --namespace test redis -o jsonpath="{.data.redis-password}" | base64 --decode)

helm install stable/redis-ha --set "persistentVolume.storageClass=managed-premium" --set "fullnameOverride=exceptionless-redis" --name exceptionless-redis
kubectl exec -it redis-redis-ha-server-0 bash -n ex-prod

# install exceptionless app
API_TAG=5.0.3335-pre
helm install --name exceptionless --namespace ex-prod ./exceptionless \
    --set "storage.azureConnectionString=DefaultEndpointsProtocol=https;AccountName=ex4events;AccountKey=$AZURE_ACCOUNT_KEY;EndpointSuffix=core.windows.net" \
    --set "elasticsearch.connectionString=http://10.0.0.4:9200" \
    --set "redis.connectionString=ex4-cache.redis.cache.windows.net:6380,password=$REDIS_PASSWORD,ssl=True,abortConnect=False" \
    --set "api.domain=prod-api.exceptionless.io" \
    --set "app.domain=prod-app.exceptionless.io" \
    --set "collector.domain=prod-collector.exceptionless.io" \
    --set "api.image.tag=$API_TAG" \
    --set "jobs.image.tag=$API_TAG" \
    --set "jobs.replicaCount=0"

# upgrade exceptionless app to a new docker image tag
helm upgrade --set "api.image.tag=$API_TAG" --set "jobs.image.tag=$API_TAG" --reuse-values exceptionless ./exceptionless

# create service principal for talking to k8s
ACCOUNT=`az account show -o json`
SUBSCRIPTION_ID=`echo $ACCOUNT | jq -r '.id'`
AZ_TENANT=`echo $ACCOUNT | jq -r '.tenantId'`
SERVICE_PRINCIPAL=`az ad sp create-for-rbac --role="Azure Kubernetes Service Cluster User Role" --name http://$CLUSTER-ci --scopes="/subscriptions/$SUBSCRIPTION_ID" -o json`
AZ_USERNAME=`echo $SERVICE_PRINCIPAL | jq -r '.appId'`
AZ_PASSWORD=`echo $SERVICE_PRINCIPAL | jq -r '.password'`
echo "AZ_USERNAME=$AZ_USERNAME AZ_PASSWORD=$AZ_PASSWORD AZ_TENANT=$AZ_TENANT | az login --service-principal --username \$AZ_USERNAME --password \$AZ_PASSWORD --tenant \$AZ_TENANT"

# read about cluster autoscaler
https://docs.microsoft.com/en-us/azure/aks/autoscaler

# get all pods for the exceptionless app
kubectl get pods -l app=exceptionless

# delete all pods for the exceptionless app
kubectl delete pods -l app=exceptionless

# run a shell
kubectl run -it --rm aks-ssh --image=ubuntu
# ssh to k8s node https://docs.microsoft.com/en-us/azure/aks/ssh

# update image on all deployments and cronjobs
kubectl set image deployment,cronjob -l tier=exceptionless-backend *=exceptionless/api-ci:5.0.3335-pre

# view pod log tail
kubectl logs -f exceptionless-jobs-event-posts-6c7b78d745-xd5ln

# patch cronjob json doc
kubectl patch cronjob/exceptionless-jobs-download-geoip-database -p '{"spec":{"suspend": false}}'

# get config map checksum
kubectl get configmaps exceptionless-config -o yaml | shasum -a 256 | awk '{print $1}'

# install helper tools for using kubernetes CLI
brew install kubectx

# install tool to view pod logs
brew tap johanhaleby/kubetail
brew install kubetail

az aks delete --resource-group $RESOURCE_GROUP --name $CLUSTER
