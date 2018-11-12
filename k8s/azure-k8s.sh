# TODO: Set AZURE_ACCOUNT_KEY and REDIS_PASSWORD environment variables

RESOURCE_GROUP=exceptionless-test
CLUSTER=ex-k8s-test2
VNET=test-net
SUBNET_ID="$(az network vnet subnet list --resource-group $RESOURCE_GROUP --vnet-name $VNET --query '[0].id' --output tsv)"

az aks create \
    --resource-group $RESOURCE_GROUP \
    --name $CLUSTER \
    --kubernetes-version 1.11.3 \
    --node-count 3 \
    --node-vm-size Standard_D16s_v3 \
    --network-plugin azure \
    --vnet-subnet-id $SUBNET_ID \
    --enable-addons monitoring \
    --admin-username exuser \
    --ssh-key-value ~/.ssh/exceptionless.pub \
    --location eastus \
    --docker-bridge-address 172.17.0.1/16 \
    --dns-service-ip 10.1.0.10 \
    --service-cidr 10.1.0.0/16

az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER --overwrite-existing

# create dashboard service account
kubectl create clusterrolebinding kubernetes-dashboard --clusterrole=cluster-admin --serviceaccount=kube-system:kubernetes-dashboard

# start dashboard
az aks browse --resource-group $RESOURCE_GROUP --name $CLUSTER

# install helm
brew install kubernetes-helm
kubectl apply -f helm-rbac.yaml
helm init --service-account tiller

# install nginx ingress
helm install stable/nginx-ingress --namespace kube-system --name nginx-ingress --set controller.replicaCount=2
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

# install exceptionless app
helm install --name exceptionless-test --namespace test ./exceptionless \
    --set "storage.azureConnectionString=DefaultEndpointsProtocol=https;AccountName=testex;AccountKey=$AZURE_ACCOUNT_KEY;EndpointSuffix=core.windows.net" \
    --set "elasticsearch.connectionString=http://10.0.0.4:9200" \
    --set "redis.connectionString=test-ex-cache.redis.cache.windows.net:6380\,password=$REDIS_PASSWORD\,ssl=True\,abortConnect=False" \
    --set "api.domain=test-api.exceptionless.io" \
    --set "app.domain=test-app.exceptionless.io" \
    --set "api.image.tag=305" \
    --set "jobs.image.tag=305"

# upgrade exceptionless app to a new docker image tag
helm upgrade --set "api.image.tag=305" --set "jobs.image.tag=305" --reuse-values exceptionless-test ./exceptionless

# read about cluster autoscaler
https://docs.microsoft.com/en-us/azure/aks/autoscaler

az aks delete --resource-group $RESOURCE_GROUP --name $CLUSTER
