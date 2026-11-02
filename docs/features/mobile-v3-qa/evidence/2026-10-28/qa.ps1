# Helper thu bằng chứng QA (chỉ đọc máy, không đổi code app)
# Usage: .\qa.ps1 shot <ma>            -> screenshot evidence/<ma>.png
#        .\qa.ps1 tap <x> <y>          -> input tap
#        .\qa.ps1 swipe <x1> <y1> <x2> <y2> [ms]
#        .\qa.ps1 back                 -> KEYCODE_BACK
#        .\qa.ps1 text <chuoi>         -> input text
#        .\qa.ps1 dump <ma>            -> uiautomator dump -> evidence/<ma>-tree.xml (in ra node có text/desc + bounds)
param(
    [Parameter(Position=0)][string]$Cmd,
    [Parameter(Position=1)][string]$A,
    [Parameter(Position=2)][string]$B,
    [Parameter(Position=3)][string]$C,
    [Parameter(Position=4)][string]$D,
    [Parameter(Position=5)][string]$E
)
$ev = "D:\Source\StockRadar\docs\features\mobile-v3-qa\evidence\2026-10-28"
switch ($Cmd) {
    "shot" { adb exec-out screencap -p > "$ev\$A.png"; "saved $ev\$A.png" }
    "tap"  { adb shell input tap $A $B; Start-Sleep -Milliseconds 800 }
    "swipe"{ adb shell input swipe $A $B $C $D $(if($E){$E}else{300}); Start-Sleep -Milliseconds 800 }
    "back" { adb shell input keyevent KEYCODE_BACK; Start-Sleep -Milliseconds 800 }
    "text" { adb shell input text $A; Start-Sleep -Milliseconds 800 }
    "dump" {
        adb shell uiautomator dump /sdcard/ui.xml | Out-Null
        adb pull /sdcard/ui.xml "$ev\$A-tree.xml" | Out-Null
        $xml=[xml](Get-Content "$ev\$A-tree.xml" -Raw)
        foreach ($n in $xml.SelectNodes("//node")) {
            $t=$n.GetAttribute("text"); $d=$n.GetAttribute("content-desc"); $b=$n.GetAttribute("bounds"); $cl=$n.GetAttribute("class")
            if ($t -or $d) { "$t$d | $b" }
        }
    }
    default { "unknown cmd" }
}
