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
helm upgrade --set "api.image.tag=306" --set "jobs.image.tag=306" --reuse-values exceptionless-test ./exceptionless

# create service principal for talking to k8s
ACCOUNT=`az account show -o json`
SUBSCRIPTION_ID=`echo $ACCOUNT | jq -r '.id'`
AZ_TENANT=`echo $ACCOUNT | jq -r '.tenantId'`
SERVICE_PRINCIPAL=`az ad sp create-for-rbac --role="Azure Kubernetes Service Cluster User Role" --name http://$CLUSTER-build --scopes="/subscriptions/$SUBSCRIPTION_ID" -o json`
AZ_USERNAME=`echo $SERVICE_PRINCIPAL | jq -r '.appId'`
AZ_PASSWORD=`echo $SERVICE_PRINCIPAL | jq -r '.password'`
echo "AZ_USERNAME=$AZ_USERNAME AZ_PASSWORD=$AZ_PASSWORD AZ_TENANT=$AZ_TENANT | az login --service-principal --username \$AZ_USERNAME --password \$AZ_PASSWORD --tenant \$AZ_TENANT"

# read about cluster autoscaler
https://docs.microsoft.com/en-us/azure/aks/autoscaler

az aks delete --resource-group $RESOURCE_GROUP --name $CLUSTER
87d9819d-dc55-4187-8ce7-35e9a12cb1bd