<#
.SYNOPSIS
  Scadex RemoteDesk istemcisini oturum açılışında "en yüksek ayrıcalıkla" başlatan bir Görev Zamanlayıcı görevi kurar.

.DESCRIPTION
  istemci başlangıç klasörü kısayolundan NORMAL yetkiyle açılırsa, Windows'un UIPI kuralı gereği yönetici
  olarak çalışan pencerelere (ör. Görev Yöneticisi, yükseltilmiş uygulamalar) uzaktan fare/klavye GÖNDEREMEZ. Bu görev, istemciyi her
  oturum açılışında yükseltilmiş yetkiyle (RunLevel Highest) ve UAC sormadan başlatır; böylece yönetici pencerelerine de kontrol ulaşır.

  Betik bir KEZ, yönetici PowerShell'inde çalıştırılır. Görev yalnızca -ForUser ile verilen kullanıcı için kurulur ve o kullanıcı
  interaktif oturum açınca onun oturumunda çalışır. Kurulduktan sonra her yeniden başlatmada kendiliğinden çalışır; tekrar gerekmez.

  TEK BAŞLATMA YOLU: Bu görevi kurduktan sonra başlangıç klasöründeki normal kısayolu KALDIRIN. Aksi halde oturum açılışında hem kısayol
  (normal) hem görev (yükseltilmiş) açılır; tek-örnek kilidinde normal örnek kazanırsa yükseltilmiş örnek "ikinci örnek" diye kapanır ve
  yönetici pencereleri yine kontrol edilemez.

  Kilit ekranı, UAC güvenli masaüstü ve Ctrl+Alt+Del yükseltilmiş yetkiyle de erişilemez kalır (Windows tüm uygulamalara kapatır).

.PARAMETER ExePath
  Scadex.RemoteDesk.Windows.exe yolu. Verilmezse betikle aynı klasörde, yoksa bir üst klasörde (publish kökü) aranır.

.PARAMETER ForUser
  Görevin çalışacağı kullanıcı (DOMAIN\kullanici ya da MAKINE\kullanici). Varsayılan: betiği çalıştıran kullanıcı.

.PARAMETER Uninstall
  Görevi kaldırır.

.EXAMPLE
  # Yönetici PowerShell'inde, publish klasöründe:
  .\Deploy\Install-StartupTask.ps1
  .\Install-StartupTask.ps1 -ForUser "SAHA\operator1"
  .\Install-StartupTask.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$ForUser = "$env:USERDOMAIN\$env:USERNAME",
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$TaskName = 'Scadex RemoteDesk Client'

# Yönetici olmadan görev "en yüksek ayrıcalıkla" kaydedilemez.
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Error "Bu betik yönetici PowerShell'inde çalıştırılmalı (görev 'en yüksek ayrıcalıkla' kaydedilecek)."
    exit 1
}

if ($Uninstall) {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Görev kaldırıldı: $TaskName"
    } else {
        Write-Host "Görev zaten yok: $TaskName"
    }
    exit 0
}

if (-not $ExePath) {
    # Betik publish kökünde de (exe'nin yanında) Deploy\ alt klasöründe de olabilir: ikisine de bak.
    $candidates = @(
        (Join-Path $PSScriptRoot 'Scadex.RemoteDesk.Windows.exe'),
        (Join-Path (Split-Path $PSScriptRoot -Parent) 'Scadex.RemoteDesk.Windows.exe')
    )
    $ExePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $ExePath -or -not (Test-Path $ExePath)) {
    Write-Error "Scadex.RemoteDesk.Windows.exe bulunamadı (betiğin yanında ve bir üst klasörde arandı). -ExePath ile tam yolu verin."
    exit 1
}
$ExePath = (Resolve-Path $ExePath).Path

# --tray: pencere açmadan System Tray'de başlar.
$action    = New-ScheduledTaskAction -Execute $ExePath -Argument '--tray' -WorkingDirectory (Split-Path $ExePath)
$trigger   = New-ScheduledTaskTrigger -AtLogOn -User $ForUser
# RunLevel Highest + logon trigger = yükseltilmiş başlar, UAC sormadan. Masaüstü işi olduğu için yalnızca interaktif oturumda.
$principal = New-ScheduledTaskPrincipal -UserId $ForUser -LogonType Interactive -RunLevel Highest
$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null

Write-Host "Görev kuruldu: $TaskName"
Write-Host "  exe      : $ExePath"
Write-Host "  kullanıcı: $ForUser (oturum açılışında, yükseltilmiş)"
Write-Host ""
Write-Host "YAPILACAK: Başlangıç klasöründeki normal kısayolu kaldırın (iki örnek açılmasın):"
Write-Host '  Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\Scadex*.lnk"'
Write-Host "İstemci bir sonraki oturum açılışında yükseltilmiş başlar; hemen denemek için:"
Write-Host "  Get-Process Scadex.RemoteDesk.Windows -ErrorAction SilentlyContinue | Stop-Process -Force"
Write-Host "  Start-ScheduledTask -TaskName '$TaskName'"
Write-Host "Doğrulama: günlükte 'yetki yükseltilmiş' satırı olmalı:"
Write-Host '  Get-Content "$env:LOCALAPPDATA\Scadex\RemoteDesk\logs\client-*.log" -Encoding UTF8 | Select-String "İstemci başladı" | Select-Object -Last 1'
