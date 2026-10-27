# Dùng chung cho các script: đọc D:\LumiDB\.env vào biến môi trường của tiến trình này.
$LumiDbDir = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $LumiDbDir '.env'
if (-not (Test-Path $envFile)) {
    throw "Không thấy $envFile. Chép .env.example thành .env rồi điền mật khẩu."
}
foreach ($line in Get-Content $envFile -Encoding UTF8) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') {
        Set-Item -Path "env:$($Matches[1])" -Value $Matches[2]
    }
}
if (-not $env:PGHOST) { $env:PGHOST = 'localhost' }
if (-not $env:PGPORT) { $env:PGPORT = '5432' }

# File SQL có chú thích tiếng Việt; client mặc định WIN1252 sẽ báo lỗi byte không hợp lệ
$env:PGCLIENTENCODING = 'UTF8'
# Bỏ các NOTICE kiểu "already exists, skipping"
$env:PGOPTIONS = "-c client_min_messages=warning"

$PgBin = 'C:\Program Files\PostgreSQL\18\bin'
$Psql = Join-Path $PgBin 'psql.exe'
if (-not (Test-Path $Psql)) { $Psql = 'psql' }
