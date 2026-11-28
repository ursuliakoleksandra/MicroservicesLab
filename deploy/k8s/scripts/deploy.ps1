# Розгортання: образи -> Secret-и -> маніфести. Запуск: ./deploy/k8s/scripts/deploy.ps1 -SqlPassword '...' [-Target deploy/k8s|Platform.AppHost/aspirate-output]
param([Parameter(Mandatory)][string]$SqlPassword, [string]$Target = 'deploy/k8s')
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..\..\..')
./deploy/k8s/scripts/build-images.ps1
./deploy/k8s/scripts/create-secrets.ps1 -SqlPassword $SqlPassword
kubectl apply -k $Target
kubectl -n technest rollout status deploy/gateway --timeout=300s
kubectl -n technest get pods,svc
Write-Host 'Gateway: kubectl -n technest port-forward svc/gateway 5000:8080  ->  http://localhost:5000'
