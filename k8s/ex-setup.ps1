# install kubernetes cli
choco install kubernetes-cli

# kubernetes helper tool
# https://github.com/ahmetb/kubectx/releases
# NOTE: not available on choco yet - choco install kubectx

# azure cli
choco install azure-cli

# install helm
choco install kubernetes-helm
helm repo add stable https://kubernetes-charts.storage.googleapis.com/
helm repo add jetstack https://charts.jetstack.io
helm repo update

### setup
$RESOURCE_GROUP="exceptionless-v6"
$CLUSTER="ex-k8s-v6"
$VNET="ex-net-v6"
$ENV="dev"

# it's important to have a decent sized network (reserve a /16 for each cluster).
az network vnet create -g $RESOURCE_GROUP -n $VNET --subnet-name $CLUSTER --address-prefixes 10.60.0.0/16 --subnet-prefixes 10.60.0.0/18 --location eastus
$SUBNET_ID=$(az network vnet subnet list --resource-group $RESOURCE_GROUP --vnet-name $VNET --query '[0].id' --output tsv)

# create new service principal and update the cluster to use it (seems these expire after a year)
az ad sp create-for-rbac --skip-assignment --name $CLUSTER
az aks update-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER --reset-service-principal --service-principal $SP_ID --client-secret $SP_SECRET

az aks create `
    --resource-group $RESOURCE_GROUP `
    --name $CLUSTER `
    --node-count 3 `
    --node-vm-size Standard_D8s_v3 `
    --max-pods 50 `
    --network-plugin azure `
    --vnet-subnet-id $SUBNET_ID `
    --enable-addons monitoring `
    --admin-username exuser `
    --ssh-key-value $HOME\.ssh\exceptionless.pub `
    --location eastus `
    --docker-bridge-address 172.17.0.1/16 `
    --dns-service-ip 10.60.192.10 `
    --service-cidr 10.60.192.0/18

az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER --overwrite-existing

# install dashboard
# https://github.com/kubernetes/dashboard/releases
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.0.3/aio/deploy/recommended.yaml

# create admin user to login to the dashboard
kubectl apply -f admin-service-account.yaml

# set the namespace
kubectl config set-context --current --namespace=ex-$ENV

# setup elasticsearch operator
# https://www.elastic.co/guide/en/cloud-on-k8s/current/k8s-quickstart.html
# https://github.com/elastic/cloud-on-k8s/releases
kubectl apply -f https://download.elastic.co/downloads/eck/1.2.1/all-in-one.yaml

# view ES operator logs
kubectl -n elastic-system logs -f statefulset.apps/elastic-operator

# create elasticsearch and kibana instances
kubectl apply -f ex-dev-elasticsearch.yaml
# check on deployment, wait for green
kubectl get elasticsearch
kubectl get es; kubectl get pods -l common.k8s.elastic.co/type=elasticsearch

# get elastic password into env variable
$ELASTIC_PASSWORD=$(kubectl get secret "ex-$ENV-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}')

# port forward elasticsearch
$ELASTIC_JOB = kubectl port-forward service/ex-$ENV-es-http 9200 &

# create daily snapshot repository

curl -X PUT -H "Content-Type: application/json" -k `
    -d '{ \"type\": \"azure\", \"settings\": { \"base_path\": \"dev-daily\" }}' `
    http://elastic:$ELASTIC_PASSWORD@localhost:9200/_snapshot/daily

curl -X PUT -H "Content-Type: application/json" -k `
    -d '{ \"schedule\": \"0 30 2 * * ?\", \"name\": \"<dev-daily-{now/d{yyyy.MM.dd|America/Chicago}}>\", \"repository\": \"daily\", \"config\": { \"indices\": [\"*\"] }, \"retention\": { \"expire_after\": \"30d\", \"min_count\": 5, \"max_count\": 50 }}' `
    http://elastic:$ELASTIC_PASSWORD@localhost:9200/_slm/policy/daily

# create hourly snapshot repository
curl -X PUT -H "Content-Type: application/json" -k `
    -d '{ \"type\": \"azure\", \"settings\": { \"base_path\": \"dev-hourly\" }}' `
    http://elastic:$ELASTIC_PASSWORD@localhost:9200/_snapshot/hourly

curl -X PUT -H "Content-Type: application/json" -k `
    -d '{ \"schedule\": \"0 0 * * * ?\", \"name\": \"<dev-hourly-{now/H{yyyy.MM.dd-HH|America/Chicago}}>\", \"repository\": \"hourly\", \"config\": { \"indices\": [\"*\"] }, \"retention\": { \"expire_after\": \"24h\", \"min_count\": 5, \"max_count\": 50 }}' `
    http://elastic:$ELASTIC_PASSWORD@localhost:9200/_slm/policy/hourly

Remove-Job $ELASTIC_JOB

# install nginx ingress
helm install nginx-ingress stable/nginx-ingress --namespace nginx-ingress --values nginx-values.yaml

# wait for external ip to be assigned
kubectl get service -l app=nginx-ingress --namespace nginx-ingress
$IP=$(kubectl get service -l app=nginx-ingress --namespace nginx-ingress -o=jsonpath='{.items[0].status.loadBalancer.ingress[0].ip}')
$PUBLICIPID=$(az network public-ip list --query "[?ipAddress!=null]|[?contains(ipAddress, '$IP')].[id]" --output tsv)
az network public-ip update --ids $PUBLICIPID --dns-name $CLUSTER

# install cert-manager
# https://github.com/jetstack/cert-manager/releases
kubectl apply --validate=false -f https://github.com/jetstack/cert-manager/releases/download/v0.15.1/cert-manager.crds.yaml
kubectl create namespace cert-manager
kubectl apply -f cluster-issuer.yaml
helm install cert-manager jetstack/cert-manager --namespace cert-manager --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer

# install kubecost
# https://kubecost.com/install?ref=home
kubectl create namespace kubecost
helm repo add kubecost https://kubecost.github.io/cost-analyzer/
helm install kubecost kubecost/cost-analyzer --namespace kubecost --set kubecostToken=$KUBECOST_KEY

# install goldilocks
helm repo add fairwinds-stable https://charts.fairwinds.com/stable
helm install goldilocks fairwinds-stable/goldilocks --namespace goldilocks

# TODO: update this file using the cluster name for the dns
kubectl apply -f certificates.yaml
kubectl describe certificate -n tls-secret

# apply namespace default limits
kubectl apply -f namespace-default-limits.yaml

# install redis server
helm repo add bitnami https://charts.bitnami.com/bitnami
helm install ex-$ENV-redis bitnami/redis --values ex-$ENV-redis-values.yaml --namespace ex-$ENV

# install exceptionless app
$APP_TAG="2.8.1-alpha.0.45"
$API_TAG="6.1.1-alpha.0.81"
helm install ex-$ENV .\exceptionless --namespace ex-$ENV --values ex-$ENV-values.yaml `
    --set "app.image.tag=$APP_TAG" `
    --set "api.image.tag=$API_TAG" `
    --set "jobs.image.tag=$API_TAG" `
    --set "elasticsearch.connectionString=$ELASTIC_CONNECTIONSTRING" `
    --set "email.connectionString=$EMAIL_CONNECTIONSTRING" `
    --set "queue.connectionString=$QUEUE_CONNECTIONSTRING" `
    --set "redis.connectionString=$REDIS_CONNECTIONSTRING" `
    --set "storage.connectionString=$STORAGE_CONNECTIONSTRING" `
    --set "statsd.token=$STATSD_TOKEN" `
    --set "statsd.user=$STATSD_USER" `
    --set "config.EX_ApplicationInsightsKey=$EX_ApplicationInsightsKey" `
    --set "config.EX_ConnectionStrings__OAuth=$EX_ConnectionStrings__OAuth" `
    --set "config.EX_ExceptionlessApiKey=$EX_ExceptionlessApiKey" `
    --set "config.EX_GoogleGeocodingApiKey=$EX_GoogleGeocodingApiKey" `
    --set "config.EX_GoogleTagManagerId=$EX_GoogleTagManagerId" `
    --set "config.EX_StripeApiKey=$EX_StripeApiKey" `
    --set "config.EX_StripePublishableApiKey=$EX_StripePublishableApiKey" `
    --set "config.EX_MaxMindGeoIpKey=$EX_MaxMindGeoIpKey" `
    --set "config.EX_StripeWebHookSigningSecret=$EX_StripeWebHookSigningSecret"

$ENV="dev"
$REDIS_CONNECTIONSTRING="server=ex-dev-redis\,password=veR9d6VB6Z\,abortConnect=false\,serviceName=exceptionless"
helm upgrade ex-$ENV .\exceptionless --namespace ex-$ENV --reuse-values --set "redis.connectionString=$REDIS_CONNECTIONSTRING"
helm upgrade ex-$ENV .\exceptionless --namespace ex-$ENV --reuse-values --set "app.image.repository=exceptionless/ui-ci"

# create service principal for talking to k8s
$SUBSCRIPTION_ID=$(az account show --query 'id' --output tsv)
$AZ_TENANT=$(az account show --query 'tenantId' --output tsv)
$SERVICE_PRINCIPAL=$(az ad sp create-for-rbac --role="Azure Kubernetes Service Cluster User Role" --name http://$CLUSTER-ci --scopes="/subscriptions/$SUBSCRIPTION_ID" -o json)
$AZ_USERNAME=`echo $SERVICE_PRINCIPAL | jq -r '.appId'`
$AZ_PASSWORD=`echo $SERVICE_PRINCIPAL | jq -r '.password'`
Write-Output "AZ_USERNAME=$AZ_USERNAME AZ_PASSWORD=$AZ_PASSWORD AZ_TENANT=$AZ_TENANT | az login --service-principal --username \$AZ_USERNAME --password \$AZ_PASSWORD --tenant \$AZ_TENANT"

# create new service principal and update the cluster to use it (seems these expire after a year)
# https://docs.microsoft.com/en-us/azure/aks/update-credentials
$SERVICE_PRINCIPAL=$(az ad sp create-for-rbac --skip-assignment --name $CLUSTER -o json)
$AZ_USERNAME=$(Write-Output $SERVICE_PRINCIPAL | jq -r '.appId')
$AZ_PASSWORD=$(Write-Output $SERVICE_PRINCIPAL | jq -r '.password')
az aks update-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER --reset-service-principal --service-principal $AZ_USERNAME --client-secret $AZ_PASSWORD

# renew service principal
az ad sp credential reset --name "$CLUSTER-ci" --years 2

# delete the entire thing
az aks delete --resource-group $RESOURCE_GROUP --name $CLUSTER

# https://support.binarylane.com.au/support/solutions/articles/1000055889-how-to-benchmark-disk-i-o

# https://github.com/FairwindsOps/goldilocks
# https://www.fairwinds.com/news/introducing-goldilocks-a-tool-for-recommending-resource-requests
# kubectl -n goldilocks port-forward svc/goldilocks-dashboard 8080:80

# kubectl port-forward --namespace kubecost deployment/kubecost-cost-analyzer 9090

# think about using this instead of kubecost
# https://github.com/helm/charts/tree/master/stable/prometheus-operator

### TODO
# monitor resource usages over next week
# set resource request and limits
# scale ES up to 4 nodes
# change VM reserved instances to 5 instead of 4

# setup powershell alias to connect kibana
# add these to your $PROFILE
Function ExDevKibanaConnect { start-process http://kibana-ex-dev.localtest.me:5650 && kubectl port-forward --namespace ex-dev --context ex service/ex-dev-kb-http 5650:5601 }
New-Alias Ex-Dev-Connect ExDevKibanaConnect

New-Alias k kubectl