# Test script to apply Schedule migrations one by one on an empty database
# This verifies that all migrations work correctly in sequence

$ErrorActionPreference = "Stop"

$testDbPath = Join-Path $env:TEMP "test_schedule_$(New-Guid).db"
$migrations = @(
    "20190922162956_InitialCreate",
    "20191101225511_Add_General_Settings_Table",
    "20191101234748_Add_Number_Of_Chapters_To_Read_Column",
    "20191104225343_Add_FinishedDuration_Column",
    "20200330025928_AddAlarmNotificationsTable",
    "20210408045321_Add_NotificationEnabled_Column",
    "20260111001000_UpdateToNextRelease"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing Schedule Migrations" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test database: $testDbPath" -ForegroundColor Yellow
Write-Host ""

# Clean up any existing test database
if (Test-Path $testDbPath) {
    Remove-Item $testDbPath -Force
    Write-Host "Cleaned up existing test database" -ForegroundColor Gray
}

# Set environment variable to use test database
$env:TEST_DB_PATH = $testDbPath

$projectPath = Join-Path $PSScriptRoot "Bible.Alarm.DbMigration.csproj"
$context = "ScheduleDbContext"

$successCount = 0
$failureCount = 0

foreach ($migration in $migrations) {
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    Write-Host "Applying migration: $migration" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    
    try {
        # Update database to this specific migration
        $result = dotnet ef database update $migration `
            --project $projectPath `
            --context $context `
            --connection "Data Source=$testDbPath" `
            2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Migration applied successfully" -ForegroundColor Green
            
            # Verify migration was applied
            $statusResult = dotnet ef migrations list `
                --project $projectPath `
                --context $context `
                --connection "Data Source=$testDbPath" `
                2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Migration status:" -ForegroundColor Gray
                Write-Host $statusResult
            }
            
            $successCount++
        } else {
            Write-Host "✗ Migration failed!" -ForegroundColor Red
            Write-Host $result
            $failureCount++
            break
        }
    }
    catch {
        Write-Host "✗ Error applying migration: $_" -ForegroundColor Red
        $failureCount++
        break
    }
    
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $failureCount" -ForegroundColor $(if ($failureCount -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failureCount -eq 0) {
    Write-Host "✓ All migrations applied successfully!" -ForegroundColor Green
    
    # Final verification - check if all migrations are applied
    Write-Host ""
    Write-Host "Final verification..." -ForegroundColor Yellow
    $finalStatus = dotnet ef migrations list `
        --project $projectPath `
        --context $context `
        --connection "Data Source=$testDbPath" `
        2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host $finalStatus
    }
} else {
    Write-Host "✗ Some migrations failed!" -ForegroundColor Red
    exit 1
}

# Clean up test database
Write-Host ""
Write-Host "Cleaning up test database..." -ForegroundColor Gray
if (Test-Path $testDbPath) {
    Remove-Item $testDbPath -Force
    Write-Host "✓ Test database removed" -ForegroundColor Green
}

Write-Host ""
Write-Host "Test completed successfully!" -ForegroundColor Green
