param([string]$App = 'ArrowVortex', [string]$Action = 'Capture', [string]$Path, [string]$Keys, [int]$X, [int]$Y)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TaskWindow {
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int left, top, right, bottom; }
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetLastActivePopup(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 delegate bool EnumProc(IntPtr h, IntPtr p);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr p);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int count);
 public static IntPtr FindDialog(uint process) {
  IntPtr result = IntPtr.Zero;
  EnumWindows((h,p) => { uint id; GetWindowThreadProcessId(h, out id);
   var c = new System.Text.StringBuilder(128); GetClassName(h,c,128);
   if (id == process && IsWindowVisible(h) && c.ToString() == "#32770") result=h;
   return true;
  },IntPtr.Zero);
  return result;
 }
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int t,bool repaint);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
}
'@
[TaskWindow]::SetProcessDPIAware() | Out-Null
$target = Get-Process -Name $App | Where-Object { $_.MainWindowHandle -ne 0 } | Sort-Object StartTime -Descending | Select-Object -First 1
if (!$target) { throw "No window for $App" }
$hwnd = $target.MainWindowHandle
[TaskWindow]::ShowWindow($hwnd, 9) | Out-Null
[IntPtr]$popup = [TaskWindow]::GetLastActivePopup($hwnd)
if ($popup -ne [IntPtr]::Zero) { $hwnd = $popup }
$dialog = [TaskWindow]::FindDialog([uint32]$target.Id)
if ($dialog -ne [IntPtr]::Zero) { $hwnd = $dialog }
[TaskWindow]::ShowWindow($hwnd, 9) | Out-Null
[uint32]$foregroundProcess = 0
$foregroundThread = [TaskWindow]::GetWindowThreadProcessId([TaskWindow]::GetForegroundWindow(), [ref]$foregroundProcess)
$currentThread = [TaskWindow]::GetCurrentThreadId()
if ($foregroundThread -ne $currentThread) { [TaskWindow]::AttachThreadInput($currentThread, $foregroundThread, $true) | Out-Null }
[TaskWindow]::SetForegroundWindow($hwnd) | Out-Null
if ($foregroundThread -ne $currentThread) { [TaskWindow]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null }
Start-Sleep -Milliseconds 250
[TaskWindow]::GetWindowThreadProcessId([TaskWindow]::GetForegroundWindow(), [ref]$foregroundProcess) | Out-Null
if ($foregroundProcess -ne $target.Id) {
 $windowShell = New-Object -ComObject WScript.Shell
 $windowShell.AppActivate($target.Id) | Out-Null
 Start-Sleep -Milliseconds 400
 [TaskWindow]::GetWindowThreadProcessId([TaskWindow]::GetForegroundWindow(), [ref]$foregroundProcess) | Out-Null
}
if ($foregroundProcess -ne $target.Id) { throw 'Target window could not be focused. No input or screenshot was sent.' }
if ($Action -eq 'Keys') { [Windows.Forms.SendKeys]::SendWait($Keys) }
elseif ($Action -eq 'Paste') { [Windows.Forms.Clipboard]::SetText($Keys); [Windows.Forms.SendKeys]::SendWait('^v') }
elseif ($Action -eq 'Physical') {
 foreach ($item in $Keys.Split(',')) {
  $parts = $item.Trim().Split(':')
  $code = [byte][int][Enum]::Parse([Windows.Forms.Keys], $parts[0], $true)
  if ($parts.Length -eq 1 -or $parts[1] -eq 'down') { [TaskWindow]::keybd_event($code,0,0,[UIntPtr]::Zero) }
  Start-Sleep -Milliseconds 35
  if ($parts.Length -eq 1 -or $parts[1] -eq 'up') { [TaskWindow]::keybd_event($code,0,2,[UIntPtr]::Zero) }
  Start-Sleep -Milliseconds 35
 }
}
elseif ($Action -eq 'Position') { [TaskWindow]::MoveWindow($hwnd,100,100,1100,800,$true) | Out-Null }
elseif ($Action -eq 'Click') {
 $rect = New-Object TaskWindow+Rect
 [TaskWindow]::GetWindowRect($hwnd,[ref]$rect) | Out-Null
 [TaskWindow]::SetCursorPos($rect.left+$X,$rect.top+$Y) | Out-Null
 [TaskWindow]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 80
 [TaskWindow]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 200
}
elseif ($Action -eq 'Capture') {
 $rect = New-Object TaskWindow+Rect
 [TaskWindow]::GetWindowRect($hwnd,[ref]$rect) | Out-Null
 $bmp = [Drawing.Bitmap]::new($rect.right-$rect.left,$rect.bottom-$rect.top)
 $g = [Drawing.Graphics]::FromImage($bmp)
 $g.CopyFromScreen($rect.left,$rect.top,0,0,$bmp.Size)
 $bmp.Save($Path)
 $g.Dispose(); $bmp.Dispose()
 Write-Output $Path
}
