#!/usr/bin/env bash
# Розгортання: образи -> Secret-и -> маніфести. Запуск: SQL_PASSWORD='...' ./deploy/k8s/scripts/deploy.sh [deploy/k8s|Platform.AppHost/aspirate-output]
set -euo pipefail
cd "$(dirname "$0")/../../.."
TARGET=${1:-deploy/k8s}
./deploy/k8s/scripts/build-images.sh
./deploy/k8s/scripts/create-secrets.sh
kubectl apply -k "$TARGET"
kubectl -n technest rollout status deploy/gateway --timeout=300s
kubectl -n technest get pods,svc
echo "Gateway: kubectl -n technest port-forward svc/gateway 5000:8080  ->  http://localhost:5000"
