# show elasticsearch password
kubectl get secret --namespace ex-prod "ex-prod-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}'

# connect to kibana
kubectl port-forward --namespace ex-prod service/ex-prod-kb-http 5601
open "http://kibana-ex-prod.localtest.me:5601"

# port forward elasticsearch
kubectl port-forward --namespace ex-prod service/ex-prod-es-http 9200

# connect to redis
REDIS_PASSWORD=$(kubectl get secret --namespace ex-prod ex-prod-redis -o jsonpath="{.data.redis-password}" | base64 --decode)
kubectl port-forward --namespace ex-prod service/ex-prod-redis-master 6379
redis-cli -a $REDIS_PASSWORD

# open kubernetes dashboard
kubectl -n kubernetes-dashboard describe secret $(kubectl -n kubernetes-dashboard get secret | grep admin-user | awk '{print $1}')
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
kubectl get es && k get pods --namespace ex-prod -l common.k8s.elastic.co/type=elasticsearch

# manually run a job
kubectl run --namespace ex-prod ex-prod-client --rm --tty -i --restart='Never' \
    --env ELASTIC_PASSWORD=$ELASTIC_PASSWORD \
    --image exceptionless/api-ci:$API_TAG -- bash

# upgrade nginx ingress to latest
# https://github.com/kubernetes/ingress-nginx/releases
helm repo update
helm upgrade --reset-values --namespace nginx-ingress -f nginx-values.yaml nginx-ingress stable/nginx-ingress --dry-run

# upgrade cert-manager
# https://github.com/jetstack/cert-manager/releases
helm repo update
kubectl apply --validate=false -f https://github.com/jetstack/cert-manager/releases/download/v0.15.1/cert-manager.crds.yaml
helm upgrade cert-manager jetstack/cert-manager --namespace cert-manager --reset-values --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer --dry-run

# upgrade dashboard
# https://github.com/kubernetes/dashboard/releases
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.0.1/aio/deploy/recommended.yaml

# upgrade kubecost
helm repo update
helm upgrade kubecost kubecost/cost-analyzer --namespace kubecost --reset-values --set kubecostToken="ZXNtaXRoQHNsaWRlcm9vbS5jb20=xm343yadf98" --dry-run

# upgrade goldilocks
helm repo update
helm upgrade goldilocks fairwinds-stable/goldilocks --namespace goldilocks --reset-values --dry-run

# upgrade elasticsearch operator
# https://www.elastic.co/guide/en/cloud-on-k8s/current/k8s-quickstart.html
# https://github.com/elastic/cloud-on-k8s/releases
kubectl apply -f https://download.elastic.co/downloads/eck/1.1.2/all-in-one.yaml

# upgrade elasticsearch
kubectl apply -f ex-prod-elasticsearch.yaml

# upgrade exceptionless app to a new docker image tag
APP_TAG="2.8.1502-pre"
API_TAG="6.0.3534-pre"
helm upgrade --set "api.image.tag=$API_TAG" --set "jobs.image.tag=$API_TAG" --reuse-values ex-prod ./exceptionless
helm upgrade --reuse-values ex-prod ./exceptionless
# see what an upgrade will do
helm diff upgrade --reuse-values ex-prod ./exceptionless

# stop the entire app
kubectl scale deployment/ex-prod-api --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-collector --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-close-inactive-sessions --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-daily-summary --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-notifications --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-posts --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-user-descriptions --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-mail-message --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-cleanup-data --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-stack-event-count --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-web-hooks --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-work-item --replicas=0 --namespace ex-prod
kubectl scale deployment/ex-prod-statsd --replicas=0 --namespace ex-prod

kubectl patch cronjob/ex-prod-jobs-cleanup-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-download-geoip-database -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-event-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-maintain-indexes -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-organization-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-stack-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-prod

# resume the app
kubectl scale deployment/ex-prod-api --replicas=5 --namespace ex-prod
kubectl scale deployment/ex-prod-collector --replicas=12 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-close-inactive-sessions --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-daily-summary --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-notifications --replicas=2 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-posts --replicas=6 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-event-user-descriptions --replicas=2 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-mail-message --replicas=2 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-cleanup-data --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-stack-event-count --replicas=1 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-web-hooks --replicas=4 --namespace ex-prod
kubectl scale deployment/ex-prod-jobs-work-item --replicas=5 --namespace ex-prod
kubectl scale deployment/ex-prod-statsd --replicas=1 --namespace ex-prod

kubectl patch cronjob/ex-prod-jobs-cleanup-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-download-geoip-database -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-event-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-maintain-indexes -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-organization-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-prod
kubectl patch cronjob/ex-prod-jobs-stack-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-prod
