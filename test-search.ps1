param([string]$query)

try {
    $response = Invoke-WebRequest -Uri "http://localhost:8081/search?query=$query" -ErrorAction Stop

    Write-Output "=== Testing search for: $query ==="
    Write-Output "Status: $($response.StatusCode)"

    # Extract search result titles
    $titles = $response.Content | Select-String -Pattern '<h3[^>]*>\s*<a[^>]*>([^<]+)</a>' -AllMatches

    if ($titles.Matches.Count -gt 0) {
        Write-Output "`nFOUND $($titles.Matches.Count) results:"
        foreach ($match in $titles.Matches) {
            Write-Output "  - $($match.Groups[1].Value)"
        }
        return $true
    } else {
        Write-Output "`nWARNING: No results found for '$query'"
        return $false
    }
} catch {
    Write-Output "ERROR: $_"
    return $false
}
