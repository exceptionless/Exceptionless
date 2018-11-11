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

helm repo add azure-samples https://azure-samples.github.io/helm-charts/
helm install azure-samples/aks-helloworld
helm install azure-samples/aks-helloworld --set title="AKS Ingress Demo" --set serviceName="ingress-demo"
kubectl apply -f hello-world-ingress.yaml

https://ex-k8s-test2.eastus.cloudapp.azure.com/
https://ex-k8s-test2.eastus.cloudapp.azure.com/hello-world-two

az aks delete --resource-group $RESOURCE_GROUP --name $CLUSTER
