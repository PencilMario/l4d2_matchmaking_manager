param(
    [string]$Challenge = '0x12345678',
    [string]$Cookie = '0x0186000047CF0FD8',
    [int]$HostVersion = 2243
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$publishDirectory = Join-Path $PSScriptRoot 'publish-reservation-test'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'

Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public sealed class ReservationUdpFixture : IDisposable
{
    private static readonly byte[] ChallengeResponse = Convert.FromHexString(
        "FFFFFFFF41C40B09030300000000001730FEDE21C7400101726573657276653030303030303000");

    private readonly UdpClient udp;
    private readonly Task worker;
    private readonly string mode;

    public ReservationUdpFixture(string mode)
    {
        this.mode = mode;
        udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        Port = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
        worker = Task.Run(Run);
    }

    public int Port { get; }

    public void Dispose()
    {
        udp.Dispose();
        try
        {
            worker.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException exception) when (
            exception.InnerException is ObjectDisposedException
            || exception.InnerException is SocketException)
        {
        }
    }

    private void Run()
    {
        try
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            udp.Receive(ref remote);
            if (mode == "challenge-timeout")
            {
                Thread.Sleep(1000);
                return;
            }

            var challenge = (byte[])ChallengeResponse.Clone();
            if (mode == "invalid-challenge")
                challenge[4] = 0x42;
            udp.Send(challenge, challenge.Length, remote);
            if (mode == "invalid-challenge")
                return;

            udp.Receive(ref remote);
            if (mode == "reservation-timeout")
            {
                Thread.Sleep(1000);
                return;
            }

            var response = mode switch
            {
                "accepted" => "FFFFFFFF70C308000001",
                "accepted-padding" => "FFFFFFFF70C308000057",
                "rejected" => "FFFFFFFF70C308000000",
                "rejected-padding" => "FFFFFFFF70C308000056",
                "host-mismatch" => "FFFFFFFF70C408000001",
                "invalid-trailing" => "FFFFFFFF70C308000001AA",
                _ => throw new InvalidOperationException("Unknown fixture mode: " + mode),
            };
            var bytes = Convert.FromHexString(response);
            udp.Send(bytes, bytes.Length, remote);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }
}
'@

function Invoke-LiveFixture(
    [string]$Mode,
    [int]$ExpectedExitCode,
    [string]$ExpectedOutput
) {
    $fixture = [ReservationUdpFixture]::new($Mode)
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $output = (& $probe reserve-server "127.0.0.1:$($fixture.Port)" $Cookie 150 $HostVersion 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorActionPreference

        if ($exitCode -ne $ExpectedExitCode) {
            throw "Fixture '$Mode' returned $exitCode, expected $ExpectedExitCode.`n$output"
        }
        if ($output -notmatch $ExpectedOutput) {
            throw "Fixture '$Mode' output did not match '$ExpectedOutput'.`n$output"
        }
    }
    finally {
        $fixture.Dispose()
    }
}

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$output = (& $probe reservation-vector $Challenge $Cookie $HostVersion 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
if ($probeExitCode -ne 0) {
    throw "Reservation vector probe failed with exit code $probeExitCode.`n$output"
}

$expectations = @(
    '(?m)^ChallengeRequest=FFFFFFFF71726573657276653030303030303000\r?$',
    '(?m)^ChallengeResponse challenge=0x03090BC4 auth_protocol=3 server_steam_id=0x0140C721DEFE3017 secure=True context=reserve0000000\r?$',
    '(?m)^IceKey=6A98CC4C54B2ACB8\r?$',
    '(?m)^PlainPayload=EFBEEDFED80FCF470000860100000000\r?$',
    '(?m)^EncryptedPayload=04944D6A637033FE8CD590BB4FE70B99\r?$',
    '(?m)^ReservationRequest=FFFFFFFF6EC30800001000000004944D6A637033FE8CD590BB4FE70B99\r?$',
    '(?m)^ReservationResponse host_version=2243 accepted=True\r?$'
)

foreach ($expectation in $expectations) {
    if ($output -notmatch $expectation) {
        throw "Missing expected output matching: $expectation`n$output"
    }
}

Invoke-LiveFixture 'accepted' 0 '(?m)^ReservationResponse .*accepted=True '
Invoke-LiveFixture 'accepted-padding' 0 '(?m)^ReservationResponse .*accepted=True '
Invoke-LiveFixture 'rejected' 7 '(?m)^ReservationResponse .*accepted=False '
Invoke-LiveFixture 'rejected-padding' 7 '(?m)^ReservationResponse .*accepted=False '
Invoke-LiveFixture 'host-mismatch' 6 '(?m)^ReservationResponse .*host_version=2244 '
Invoke-LiveFixture 'challenge-timeout' 2 '(?m)^ReservationError type=SocketException '
Invoke-LiveFixture 'invalid-challenge' 2 '(?m)^ReservationError type=InvalidDataException '
Invoke-LiveFixture 'reservation-timeout' 8 '(?m)^ReservationResponse timeout=True request_sent=True verification=server_status_required\r?$'
Invoke-LiveFixture 'invalid-trailing' 2 '(?m)^ReservationError type=InvalidDataException '

Write-Output 'L4D2 reservation protocol vectors verified.'
