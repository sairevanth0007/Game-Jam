# Sends Lightkeeper OSC messages to the running game (no console, no installs: plain .NET UdpClient).
# Start the game with OSC enabled first:  Builds\visual\LumenWave.exe -lk-osc 8000   (then title -> LIGHTKEEPER)
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/osc-test.ps1 -Address /lamp/1/on    -Value 1
#   powershell -ExecutionPolicy Bypass -File tools/osc-test.ps1 -Address /lamp/1/x     -Value 30.5
#   powershell -ExecutionPolicy Bypass -File tools/osc-test.ps1 -Address /lamp/1/color -Value 2
# Integers (no decimal point) are sent as OSC int32 (,i), anything else as float32 (,f).
param([string]$Address = '/lamp/1/on', [string]$Value = '1', [string]$HostName = '127.0.0.1', [int]$Port = 8000)

function Pad4([byte[]]$b) {
    $n = $b.Length + 1                      # OSC strings are NUL-terminated
    $pad = (4 - ($n % 4)) % 4
    return $b + [byte[]](,0 * (1 + $pad))
}
function BigEndian([byte[]]$b) { if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($b) }; return $b }

$addr = Pad4 ([Text.Encoding]::ASCII.GetBytes($Address))
$inv = [Globalization.CultureInfo]::InvariantCulture
if ($Value -match '^-?\d+$') {
    $tag = Pad4 ([Text.Encoding]::ASCII.GetBytes(',i'))
    $arg = BigEndian ([BitConverter]::GetBytes([int]::Parse($Value, $inv)))
} else {
    $tag = Pad4 ([Text.Encoding]::ASCII.GetBytes(',f'))
    $arg = BigEndian ([BitConverter]::GetBytes([single]::Parse($Value, $inv)))
}
[byte[]]$packet = $addr + $tag + $arg
$udp = New-Object System.Net.Sockets.UdpClient
try { [void]$udp.Send($packet, $packet.Length, $HostName, $Port) } finally { $udp.Close() }
Write-Host ("sent {0} {1} -> {2}:{3} ({4} bytes)" -f $Address, $Value, $HostName, $Port, $packet.Length)
