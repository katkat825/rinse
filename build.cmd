@echo off
setlocal
set ROOT=%~dp0
set LOG=%ROOT%build.log
cd /d "%ROOT%src"
taskkill /im Rinse.exe /f >nul 2>&1
echo === Rinse build %DATE% %TIME% === > "%LOG%"
dotnet --list-sdks >> "%LOG%" 2>&1
set MAJ=
for /f "tokens=1 delims=." %%v in ('dotnet --version 2^>nul') do set MAJ=%%v
if "%MAJ%"=="" (
  echo NO_DOTNET_SDK >> "%LOG%"
  goto :end
)
echo Using SDK major %MAJ% >> "%LOG%"
dotnet publish Rinse.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "%ROOT%app" >> "%LOG%" 2>&1
echo PUBLISH_EXIT %ERRORLEVEL% >> "%LOG%"
if exist "%ROOT%app\Rinse.exe" (
  "%ROOT%app\Rinse.exe" --selftest "%ROOT%selftest.txt"
  echo SELFTEST_EXIT %ERRORLEVEL% >> "%LOG%"
)
:end
echo DONE >> "%LOG%"
endlocal
