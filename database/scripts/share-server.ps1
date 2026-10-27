# Cho bạn bè chơi qua internet: chạy LumiServer (Production, chỉ nghe localhost:8080) rồi mở
# Cloudflare quick tunnel tới nó. Không mở cổng nào trên router hay tường lửa.
# In ra địa chỉ https://....trycloudflare.com và chép vào clipboard. Địa chỉ đổi mỗi lần chạy.
# Ctrl+C trong cửa sổ này tắt cả tunnel và server.
#   powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\share-server.ps1
param(
    [string]$ServerDir = 'D:\Lumi\src\Lumi.Api',
    [int]$Port = 8080
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_env.ps1')

$cloudflared = Join-Path $LumiDbDir 'tools\cloudflared.exe'
if (-not (Test-Path $cloudflared)) { throw "Không thấy $cloudflared" }
$localUrl = "http://localhost:$Port"

if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
    throw "Cổng $Port đang có chương trình khác dùng (có thể là server đang chạy bằng dotnet run). Tắt nó trước."
}

# Khóa ký token: lần đầu tự tạo 64 ký tự hex ngẫu nhiên và ghi vào .env (không in ra)
if (-not $env:LUMI_JWT_KEY) {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $env:LUMI_JWT_KEY = -join ($bytes | ForEach-Object { $_.ToString('x2') })
    $envFile = Join-Path $LumiDbDir '.env'
    $lines = @(Get-Content $envFile -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*LUMI_JWT_KEY\s*=' })
    $lines += "LUMI_JWT_KEY=$env:LUMI_JWT_KEY"
    Set-Content -Path $envFile -Value $lines -Encoding UTF8
    Write-Host 'Đã tạo LUMI_JWT_KEY mới trong .env'
}

Write-Host 'Đang build LumiServer...'
& dotnet build $ServerDir -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build LumiServer lỗi' }
$dll = Get-ChildItem (Join-Path $ServerDir 'bin\Release') -Recurse -Filter 'Lumi.Api.dll' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $dll) { throw 'Không thấy Lumi.Api.dll sau khi build' }

# Production: không có endpoint dev (inbox/dev-item), không đọc appsettings.Development.json
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:Urls = $localUrl
$env:ConnectionStrings__Lumi = "Host=$env:PGHOST;Port=$env:PGPORT;Database=lumi;Username=lumi_app;Password=$env:LUMI_APP_PASSWORD"
$env:Jwt__Key = $env:LUMI_JWT_KEY

$server = $null
$tunnel = $null
$tunnelLog = Join-Path $env:TEMP 'lumi-cloudflared.log'
try {
    $server = Start-Process dotnet -ArgumentList "`"$($dll.FullName)`"" -WorkingDirectory $ServerDir -PassThru

    Write-Host 'Đang chờ server...'
    $ready = $false
    for ($i = 0; $i -lt 60 -and -not $ready; $i++) {
        if ($server.HasExited) { throw 'Server tắt ngay khi khởi động. Xem cửa sổ server (thiếu migration? sai mật khẩu?).' }
        try { $ready = (Invoke-WebRequest "$localUrl/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep 1 }
    }
    if (-not $ready) { throw "Server không trả lời $localUrl/health sau 60 giây" }

    Write-Host 'Đang mở tunnel...'
    if (Test-Path $tunnelLog) { Remove-Item $tunnelLog }
    $tunnel = Start-Process $cloudflared -ArgumentList "tunnel --url $localUrl --no-autoupdate --logfile `"$tunnelLog`"" `
        -WindowStyle Hidden -PassThru

    $publicUrl = $null
    for ($i = 0; $i -lt 60 -and -not $publicUrl; $i++) {
        Start-Sleep 1
        if ($tunnel.HasExited) { throw "cloudflared tắt. Xem $tunnelLog" }
        if (Test-Path $tunnelLog) {
            $m = Select-String -Path $tunnelLog -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' | Select-Object -First 1
            if ($m) { $publicUrl = $m.Matches[0].Value }
        }
    }
    if (-not $publicUrl) { throw "Không lấy được địa chỉ tunnel. Xem $tunnelLog" }

    Set-Clipboard -Value $publicUrl
    Write-Host ''
    Write-Host '==================================================================' -ForegroundColor Green
    Write-Host "   $publicUrl" -ForegroundColor Green
    Write-Host '==================================================================' -ForegroundColor Green
    Write-Host 'Đã chép vào clipboard. Gửi địa chỉ này cho bạn bè; họ dán vào ô server ở màn đăng nhập.'
    Write-Host 'Giữ cửa sổ này mở. Ctrl+C để tắt cả tunnel và server.'

    while (-not $server.HasExited -and -not $tunnel.HasExited) { Start-Sleep 2 }
    if ($server.HasExited) { Write-Host 'Server đã tắt.' -ForegroundColor Yellow }
    if ($tunnel.HasExited) { Write-Host "Tunnel đã tắt. Xem $tunnelLog" -ForegroundColor Yellow }
}
finally {
    foreach ($p in @($tunnel, $server)) {
        if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    }
    Write-Host 'Đã tắt tunnel và server.'
}
