@echo off
REM ======================================================================
REM  Zander's Hoop Shrines - one-click Windows build
REM  Double-click this file. It finds Unity (installed through Unity Hub),
REM  builds the game and opens the folder with ZanderHoopShrines.exe.
REM  Close the project in the Unity editor first (Unity can't open it twice).
REM  To use a specific Unity, set UNITY_EXE to the full path of Unity.exe.
REM ======================================================================
setlocal EnableDelayedExpansion
set "PROJECT=%~dp0unity\ZanderHoopShrines"
set "LOG=%PROJECT%\Builds\build.log"
if not exist "%PROJECT%\Builds" mkdir "%PROJECT%\Builds"

if defined UNITY_EXE goto :have_unity

REM 1) the exact version the project was made with
for /f "tokens=2" %%V in ('findstr /b "m_EditorVersion:" "%PROJECT%\ProjectSettings\ProjectVersion.txt"') do set "WANT=%%V"
for %%R in ("%ProgramFiles%\Unity\Hub\Editor" "%ProgramFiles%\Unity Hub\Editor" "%LOCALAPPDATA%\Unity\Hub\Editor") do (
    if not defined UNITY_EXE if exist "%%~R\!WANT!\Editor\Unity.exe" set "UNITY_EXE=%%~R\!WANT!\Editor\Unity.exe"
)
REM 2) otherwise the newest Unity 6 / 2022.3+ that is installed
if not defined UNITY_EXE (
    for %%R in ("%ProgramFiles%\Unity\Hub\Editor" "%ProgramFiles%\Unity Hub\Editor" "%LOCALAPPDATA%\Unity\Hub\Editor") do (
        if exist "%%~R" for /f "delims=" %%D in ('dir /b /ad /on "%%~R" 2^>nul') do (
            if exist "%%~R\%%D\Editor\Unity.exe" set "UNITY_EXE=%%~R\%%D\Editor\Unity.exe"
        )
    )
)

:have_unity
if not defined UNITY_EXE (
    echo Could not find Unity. Install Unity 2022.3 LTS or Unity 6 with Unity Hub,
    echo or set UNITY_EXE to the full path of Unity.exe and run this again.
    pause
    exit /b 1
)

echo.
echo  Building Zander's Hoop Shrines with:
echo    %UNITY_EXE%
echo  This takes a few minutes (longer the very first time)...
echo.
"%UNITY_EXE%" -batchmode -quit -projectPath "%PROJECT%" -buildTarget Win64 -executeMethod ZanderGame.EditorTools.ZanderBuild.BuildWindowsCI -logFile "%LOG%"
if errorlevel 1 (
    echo.
    echo  BUILD FAILED. Details are in:
    echo    %LOG%
    echo  Common fixes: close the project in the Unity editor, or install
    echo  "Windows Build Support" for this Unity version in Unity Hub.
    pause
    exit /b 1
)
echo.
echo  Done! The game is in: %PROJECT%\Builds\Windows
echo  (and zipped at %PROJECT%\Builds\ZanderHoopShrines-Windows.zip)
explorer "%PROJECT%\Builds\Windows"
