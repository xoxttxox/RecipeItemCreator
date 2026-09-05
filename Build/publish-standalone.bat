@echo off
setlocal
cd /d "%~dp0\.."

echo =====================================================
echo  Recipe Item Creator - STANDALONE SINGLE EXE
 echo =====================================================
echo.
echo No .NET runtime installation required, but the EXE is much larger.
echo.

dotnet publish RecipeItemCreator.csproj -c Release -r win-x64 --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:PublishReadyToRun=false ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:DebugType=None ^
  /p:DebugSymbols=false ^
  -o publish-standalone

if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

echo.
echo Finished: publish-standalone\RecipeItemCreator.exe
pause
