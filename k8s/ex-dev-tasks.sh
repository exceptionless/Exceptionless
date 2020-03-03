# show elasticsearch password
kubectl get secret "ex-dev-es-elastic-user" -o go-template='{{.data.elastic | base64decode }}'

# connect to kibana
kubectl port-forward service/ex-dev-kb-http 5601
open "http://kibana-ex-dev.localtest.me:5601"

# port forward elasticsearch
kubectl port-forward service/ex-dev-es-http 9200

# connect to redis
REDIS_PASSWORD=$(kubectl get secret --namespace ex-dev ex-dev-redis -o jsonpath="{.data.redis-password}" | base64 --decode)
kubectl port-forward service/ex-dev-redis-master 6379
redis-cli -a $REDIS_PASSWORD

# open kubernetes dashboard
kubectl -n kubernetes-dashboard describe secret $(kubectl -n kubernetes-dashboard get secret | grep admin-user | awk '{print $1}')
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
kubectl get es && k get pods -l common.k8s.elastic.co/type=elasticsearch

# manually run a job
kubectl run --namespace ex-dev ex-dev-client --rm --tty -i --restart='Never' \
    --env ELASTIC_PASSWORD=$ELASTIC_PASSWORD \
    --image exceptionless/api-ci:$API_TAG -- bash

# upgrade nginx ingress to latest
# https://github.com/kubernetes/ingress-nginx/releases
helm repo update
helm upgrade --reset-values --namespace kube-system -f nginx-values.yaml nginx-ingress stable/nginx-ingress --dry-run

# upgrade cert-manager
# https://github.com/jetstack/cert-manager/releases
helm upgrade --reset-values --namespace cert-manager cert-manager jetstack/cert-manager --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer --dry-run

# upgrade exceptionless app to a new docker image tag
APP_TAG="2.8.1502-pre"
API_TAG="6.0.3534-pre"
helm upgrade --set "api.image.tag=$API_TAG" --set "jobs.image.tag=$API_TAG" --reuse-values ex-dev ./exceptionless

# stop the entire app
kubectl scale deployment/ex-dev-api --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-collector --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-close-inactive-sessions --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-daily-summary --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-notifications --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-posts --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-user-descriptions --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-mail-message --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-retention-limits --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-stack-event-count --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-web-hooks --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-work-item --replicas=0 --namespace ex-dev
kubectl scale deployment/ex-dev-statsd --replicas=0 --namespace ex-dev

kubectl patch cronjob/ex-dev-jobs-cleanup-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-download-geoip-database -p '{"spec":{"suspend": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-event-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-maintain-indexes -p '{"spec":{"suspend": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-organization-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-stack-snapshot -p '{"spec":{"suspend": true}}' --namespace ex-dev

# resume the app
kubectl scale deployment/ex-dev-api --replicas=5 --namespace ex-dev
kubectl scale deployment/ex-dev-collector --replicas=12 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-close-inactive-sessions --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-daily-summary --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-notifications --replicas=2 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-posts --replicas=6 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-event-user-descriptions --replicas=2 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-mail-message --replicas=2 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-retention-limits --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-stack-event-count --replicas=1 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-web-hooks --replicas=4 --namespace ex-dev
kubectl scale deployment/ex-dev-jobs-work-item --replicas=5 --namespace ex-dev
kubectl scale deployment/ex-dev-statsd --replicas=1 --namespace ex-dev

kubectl patch cronjob/ex-dev-jobs-cleanup-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-download-geoip-database -p '{"spec":{"suspend": false}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-event-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-maintain-indexes -p '{"spec":{"suspend": false}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-organization-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-dev
kubectl patch cronjob/ex-dev-jobs-stack-snapshot -p '{"spec":{"suspend": false}}' --namespace ex-dev
