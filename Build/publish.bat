@echo off
setlocal
cd /d "%~dp0\.."

set "OUTPUT=publish"

echo =====================================================
echo  Recipe Item Creator - SINGLE EXE
echo =====================================================
echo.
echo Creating a standalone Windows x64 build.
echo No .NET runtime installation is required.
echo.

if exist "%OUTPUT%" (
    echo Cleaning previous publish...
    rmdir /s /q "%OUTPUT%"
)

echo.
echo Publishing...
echo.

dotnet publish RecipeItemCreator.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:PublishReadyToRun=false ^
  /p:PublishTrimmed=false ^
  /p:DebugType=None ^
  /p:DebugSymbols=false ^
  /p:GenerateDocumentationFile=false ^
  -o "%OUTPUT%"

if errorlevel 1 (
    echo.
    echo =====================================================
    echo  BUILD FAILED
    echo =====================================================
    echo.
    pause
    exit /b 1
)

if not exist "%OUTPUT%\RecipeItemCreator.exe" (
    echo.
    echo =====================================================
    echo  ERROR
    echo =====================================================
    echo.
    echo RecipeItemCreator.exe was not created.
    pause
    exit /b 1
)

echo.
echo =====================================================
echo  BUILD SUCCESSFUL
echo =====================================================
echo.
echo File:
echo %OUTPUT%\RecipeItemCreator.exe
echo.

for %%F in ("%OUTPUT%\RecipeItemCreator.exe") do (
    echo Size: %%~zF bytes
)

echo.
pause