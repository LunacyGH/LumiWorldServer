# Chạy các file trong migrations\ theo thứ tự tên, mỗi file đúng một lần, bằng role lumi_owner.
# File đã chạy ghi vào bảng schema_migrations (cùng bảng LumiServer kiểm tra lúc khởi động).
# Mỗi file và dòng ghi nhận nằm chung một transaction: lỗi giữa chừng thì không ghi gì.
#   powershell -File D:\LumiDB\scripts\migrate.ps1              (database lumi)
#   powershell -File D:\LumiDB\scripts\migrate.ps1 -Database lumi_test
param(
    [string]$Database = 'lumi'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_env.ps1')

$env:PGPASSWORD = $env:LUMI_OWNER_PASSWORD
$common = @('-U', 'lumi_owner', '-d', $Database, '-X', '-q', '-v', 'ON_ERROR_STOP=1')

& $Psql @common -c 'create table if not exists schema_migrations (version text primary key, applied_at timestamptz not null default now())'
if ($LASTEXITCODE -ne 0) { throw "Không kết nối được $Database bằng lumi_owner" }

$applied = @(& $Psql @common -tA -c 'select version from schema_migrations')
if ($LASTEXITCODE -ne 0) { throw 'Không đọc được schema_migrations' }

$files = Get-ChildItem (Join-Path $LumiDbDir 'migrations') -Filter '*.sql' | Sort-Object Name
$ran = 0
foreach ($file in $files) {
    $version = $file.BaseName
    if ($applied -contains $version) { continue }

    & $Psql @common -1 -f $file.FullName -c "insert into schema_migrations (version) values ('$version')" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Migration $version lỗi, database $Database không đổi" }
    Write-Host "Đã chạy $version trên $Database"
    $ran++
}
if ($ran -eq 0) { Write-Host "$Database đã đủ migration ($($files.Count) file)" }
