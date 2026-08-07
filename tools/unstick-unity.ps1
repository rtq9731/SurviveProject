# Unity 에디터가 모달 창에 걸려 멈췄을 때 그 창을 눌러 풀어 준다.
#
# 왜 필요한가
#   uloop는 에디터의 메인 스레드에 일을 시킨다. 그런데 모달 대화상자가 떠 있으면
#   메인 스레드가 그 창에 붙들려 있어 요청이 영영 처리되지 않는다. 밖에서 보면
#   "프로세스는 살아 있고 Responding=True인데 uloop만 타임아웃"으로 보인다.
#   에디터를 강제 종료하는 것 말고는 길이 없어 보이지만, 창의 단추를 눌러 주면
#   대개 그 자리에서 풀린다.
#
#   가장 흔한 것은 씬 저장 확인 창이다. 재생 모드를 오가면 씬이 dirty로 잡히고,
#   그 상태에서 테스트를 돌리거나 씬을 열려 하면 뜬다.
#
# 쓰는 법
#   pwsh -File tools/unstick-unity.ps1                    # 무엇이 떠 있는지 보기만 한다
#   pwsh -File tools/unstick-unity.ps1 -Click             # 안전한 기본값을 눌러 푼다
#   pwsh -File tools/unstick-unity.ps1 -Click -Button "Don't Save"
#   pwsh -File tools/unstick-unity.ps1 -Click -ProjectPath "E:\SurviveProjectClones\A"
#
# 안전
#   기본값은 "보기만"이다. -Click 을 줘야 실제로 누른다.
#   기본으로 누르는 단추는 "버리는" 쪽이다 (Don't Save / No / Cancel).
#   이 저장소의 규율이 "에이전트는 씬을 저장하지 않는다"이므로 그것이 맞다.
#   Save 를 누르고 싶으면 -Button 으로 명시해야 한다 — 실수로 저장되지 않게.

[CmdletBinding()]
param(
    # 실제로 누른다. 주지 않으면 목록만 보여 준다.
    [switch]$Click,

    # 누를 단추의 글자. 여러 개를 주면 먼저 찾은 것을 누른다.
    # 기본값은 "저장하지 않고 넘어가는" 쪽들이다.
    [string[]]$Button = @("Don't Save", "Discard", "No", "Cancel", "OK"),

    # 특정 프로젝트의 에디터만 다룬다. 비우면 떠 있는 Unity 전부.
    [string]$ProjectPath = "",

    # 창이 나타날 때까지 이만큼 기다린다(초). 0이면 지금 떠 있는 것만 본다.
    [int]$WaitSeconds = 0
)

$ErrorActionPreference = "Stop"

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class Win {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    public const uint BM_CLICK = 0x00F5;

    public static string ClassOf(IntPtr h) {
        var sb = new StringBuilder(256); GetClassName(h, sb, sb.Capacity); return sb.ToString();
    }
    public static string TextOf(IntPtr h) {
        var sb = new StringBuilder(512); GetWindowText(h, sb, sb.Capacity); return sb.ToString();
    }
    public static int PidOf(IntPtr h) { int pid; GetWindowThreadProcessId(h, out pid); return pid; }
}
"@

function Get-UnityEditorPids {
    param([string]$Filter)

    $pids = @{}
    foreach ($p in Get-Process Unity -ErrorAction SilentlyContinue) {
        $cl = (Get-CimInstance Win32_Process -Filter "ProcessId=$($p.Id)" -ErrorAction SilentlyContinue).CommandLine
        if (-not $cl) { continue }
        # 임포트 일꾼은 창이 없다. 본 에디터만 본다.
        if ($cl -match 'AssetImportWorker') { continue }
        if ($Filter -and ($cl -notlike "*$Filter*")) { continue }
        $pids[$p.Id] = $cl
    }
    return $pids
}

function Get-DialogWindows {
    param([hashtable]$Pids)

    $found = New-Object System.Collections.ArrayList
    $cb = [Win+EnumProc]{
        param($h, $l)
        if ([Win]::IsWindowVisible($h)) {
            # $pid 는 PowerShell 예약 변수라 쓸 수 없다. 이름을 달리한다.
            $procId = [Win]::PidOf($h)
            if ($Pids.ContainsKey($procId)) {
                # #32770 = 표준 대화상자 클래스. Unity의 저장 확인 창이 이것이다.
                if ([Win]::ClassOf($h) -eq "#32770") {
                    [void]$found.Add([pscustomobject]@{
                        Handle = $h; Pid = $procId; Title = [Win]::TextOf($h)
                    })
                }
            }
        }
        return $true
    }
    [void][Win]::EnumWindows($cb, [IntPtr]::Zero)
    return $found
}

function Get-DialogButtons {
    param([IntPtr]$Dialog)

    $buttons = New-Object System.Collections.ArrayList
    $cb = [Win+EnumProc]{
        param($h, $l)
        if ([Win]::ClassOf($h) -eq "Button") {
            $t = [Win]::TextOf($h)
            if ($t) { [void]$buttons.Add([pscustomobject]@{ Handle = $h; Text = $t }) }
        }
        return $true
    }
    [void][Win]::EnumChildWindows($Dialog, $cb, [IntPtr]::Zero)
    return $buttons
}

# ── 본체 ────────────────────────────────────────────────────────────

$editors = Get-UnityEditorPids -Filter $ProjectPath
if ($editors.Count -eq 0) {
    $note = if ($ProjectPath) { " (필터: $ProjectPath)" } else { "" }
    Write-Output "떠 있는 Unity 에디터가 없다$note"
    exit 0
}

$deadline = (Get-Date).AddSeconds($WaitSeconds)
do {
    $dialogs = Get-DialogWindows -Pids $editors
    if ($dialogs.Count -gt 0) { break }
    if ((Get-Date) -ge $deadline) { break }
    Start-Sleep -Milliseconds 500
} while ($true)

if ($dialogs.Count -eq 0) {
    Write-Output "모달 창 없음. 에디터 $($editors.Count)개 확인함 — 멈춤의 원인이 모달은 아니다."
    Write-Output "  (대형 리임포트 중이거나 uLoopMCP 브리지 자체가 죽었을 수 있다. 그때는 재기동이 답이다.)"
    exit 0
}

$clicked = 0
foreach ($d in $dialogs) {
    Write-Output ""
    Write-Output "모달 발견 — pid $($d.Pid) / 제목: `"$($d.Title)`""
    $buttons = Get-DialogButtons -Dialog $d.Handle
    if ($buttons.Count -eq 0) { Write-Output "  단추를 찾지 못했다"; continue }

    Write-Output "  단추: $(($buttons | ForEach-Object { '[' + $_.Text + ']' }) -join ' ')"

    if (-not $Click) { Write-Output "  (보기만 함. 누르려면 -Click)"; continue }

    # 가속키 표시(앰퍼샌드)는 글자 비교에서 뺀다. "Don't &Save" 처럼 붙어 온다.
    #
    # 주의: Replace 에 char 를 넘기면 PowerShell이 Replace(char,char) 오버로드를 골라
    # 빈 문자열 인자에서 터진다. 반드시 문자열끼리 넘겨야 한다 (2026-08-06에 실제로 당했다).
    $amp = [string][char]38
    $target = $null
    foreach ($want in $Button) {
        $target = $buttons | Where-Object { $_.Text.Replace($amp, [string]'') -eq $want } | Select-Object -First 1
        if ($target) { break }
    }
    if (-not $target) {
        Write-Output "  원하는 단추가 없다 (찾던 것: $($Button -join ', '))"
        continue
    }

    [void][Win]::SendMessage($target.Handle, [Win]::BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
    $shown = $target.Text.Replace($amp, [string]'')
    Write-Output "  눌렀다: [$shown]"
    $clicked++
}

if ($Click) {
    Write-Output ""
    Write-Output "$clicked 개를 눌렀다. uloop를 다시 시도해 보라."
    Write-Output "같은 창이 또 뜬다면 원인을 없애야 한다 — 씬이 dirty인 것이 반복되면"
    Write-Output "EditorSceneManager.OpenScene 으로 디스크에서 다시 읽어 버리는 편이 낫다."
}
