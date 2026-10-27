# Chạy một lần bằng tài khoản postgres: tạo role, database lumi và lumi_test, cấp quyền.
# Chạy lại được. Mật khẩu postgres: đặt $env:PGPASSWORD trước, hoặc psql sẽ hỏi.
#   powershell -File D:\LumiDB\scripts\setup.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_env.ps1')

$setupDir = Join-Path $LumiDbDir 'setup'
$pw = @(
    '-v', "owner_pw=$env:LUMI_OWNER_PASSWORD",
    '-v', "app_pw=$env:LUMI_APP_PASSWORD",
    '-v', "report_pw=$env:LUMI_REPORT_PASSWORD"
)

function Invoke-Psql([string]$Database, [string[]]$Arguments) {
    & $Psql -U postgres -d $Database -X -q -v ON_ERROR_STOP=1 @Arguments
    if ($LASTEXITCODE -ne 0) { throw "psql lỗi (mã $LASTEXITCODE) khi chạy trên $Database" }
}

Invoke-Psql 'postgres' ($pw + @('-f', (Join-Path $setupDir '01_roles.sql')))
Invoke-Psql 'postgres' @('-f', (Join-Path $setupDir '02_databases.sql'))
foreach ($db in 'lumi', 'lumi_test') {
    # Default privileges phải do chính lumi_owner đặt (hoặc superuser "for role lumi_owner")
    Invoke-Psql $db @('-f', (Join-Path $setupDir '03_grants.sql'))
}
Write-Host 'Đã tạo role, database và quyền. Tiếp theo: scripts\migrate.ps1'
