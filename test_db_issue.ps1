#!/usr/bin/env pwsh

# Test script to reproduce the database table issue
Write-Host "Testing database table issue..."

# Change to project directory
Set-Location "C:\Blog\mostlylucidweb"

# Try to run the application with a timeout to see the error
Write-Host "Starting application to reproduce the error..."
try {
    # Start the application process
    $process = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "Mostlylucid", "--no-launch-profile" -NoNewWindow -PassThru
    
    # Wait for 30 seconds to see the error
    Start-Sleep -Seconds 30
    
    # Kill the process if it's still running
    if (!$process.HasExited) {
        $process.Kill()
        Write-Host "Process killed after timeout"
    }
    
    Write-Host "Exit code: $($process.ExitCode)"
}
catch {
    Write-Host "Error running application: $($_.Exception.Message)"
}

Write-Host "Test completed."