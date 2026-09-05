@echo off
setlocal
cd /d "%~dp0\.."

echo =============================================
echo  Recipe Item Creator - SMALL SINGLE EXE
 echo =============================================
echo.
echo Requires the .NET 10 Desktop Runtime on the target PC.
echo This keeps the EXE much smaller.
echo.

dotnet publish RecipeItemCreator.csproj -c Release -r win-x64 --self-contained false ^
  /p:PublishSingleFile=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:PublishReadyToRun=false ^
  /p:DebugType=None ^
  /p:DebugSymbols=false ^
  -o publish-small

if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

echo.
echo Finished: publish-small\RecipeItemCreator.exe
pause
