# Script para probar el endpoint /tools
Write-Host "Probando el endpoint /tools..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/tools" -Method Get -ErrorAction Stop
    Write-Host "`nRespuesta recibida:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | Write-Host
    
    Write-Host "`n`nNúmero de herramientas: $($response.Count)" -ForegroundColor Yellow
    
    foreach ($tool in $response) {
        Write-Host "`n--- $($tool.Name) ---" -ForegroundColor Magenta
        Write-Host "Description: $($tool.Description)"
        if ($tool.Parameters) {
            Write-Host "Parameters: " -NoNewline
            $tool.Parameters | ConvertTo-Json -Compress | Write-Host
        }
    }
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Asegúrate de que el servidor esté corriendo con: dotnet run" -ForegroundColor Yellow
}
