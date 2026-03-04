# run_server.ps1
# This script builds and runs the Golang Relay Server using Docker.

$ImageName = "go-relay-server"
$ContainerName = "vas-relay-server"
$Port = "49152"

Write-Host "--- 1. Stopping and removing existing container... ---" -ForegroundColor Cyan
docker stop $ContainerName 2>$null
docker rm $ContainerName 2>$null

Write-Host "--- 2. Building Docker image... (This may take a while) ---" -ForegroundColor Cyan
docker build -t $ImageName .

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build Failed! Please check if Docker Desktop is running." -ForegroundColor Red
    exit
}

Write-Host "--- 3. Running container (UDP Port $Port) ---" -ForegroundColor Cyan
docker run -d --name $ContainerName -p "$($Port):$($Port)/udp" $ImageName

Write-Host "✅ Server started successfully!" -ForegroundColor Green
Write-Host "To view logs, run: docker logs -f $ContainerName" -ForegroundColor Yellow
