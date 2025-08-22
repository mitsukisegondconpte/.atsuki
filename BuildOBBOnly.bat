@echo off
echo =====================================================
echo        Tsuki BR Demo - Build OBB Seulement
echo =====================================================

:: Configuration
set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\2021.3.15f1\Editor\Unity.exe"
set PROJECT_PATH=%~dp0..
set BUILD_PATH=%~dp0
set OBB_NAME=main.1.com.tsuki.battleroyale.obb

echo.
echo Ce script rebuild seulement le fichier OBB
echo Utilisez ceci pour mettre à jour les assets sans recompiler l'APK
echo.

:: Vérifier si Unity existe
if not exist %UNITY_PATH% (
    echo ERREUR: Unity n'est pas trouvé dans le chemin spécifié!
    echo Veuillez modifier UNITY_PATH dans ce script.
    pause
    exit /b 1
)

echo =====================================================
echo              Build OBB en cours...
echo =====================================================

:: Lancer Unity pour build seulement l'OBB
%UNITY_PATH% -batchmode -quit ^
    -projectPath "%PROJECT_PATH%" ^
    -executeMethod AndroidBuilder.BuildOBBOnly ^
    -buildPath "%BUILD_PATH%" ^
    -logFile "%BUILD_PATH%obb_build_log.txt"

if %ERRORLEVEL% neq 0 (
    echo ERREUR: Échec du build OBB!
    echo Vérifiez le log: %BUILD_PATH%obb_build_log.txt
    pause
    exit /b 1
)

:: Vérifier OBB
if exist "%BUILD_PATH%%OBB_NAME%" (
    echo ✓ OBB régénéré: %OBB_NAME%
    for %%A in ("%BUILD_PATH%%OBB_NAME%") do echo   Taille: %%~zA octets
) else (
    echo ✗ ERREUR: OBB non généré!
    pause
    exit /b 1
)

echo.
echo =====================================================
echo             Build OBB Terminé!
echo =====================================================

echo.
echo Nouveau fichier OBB: %BUILD_PATH%%OBB_NAME%
echo.
echo Pour mettre à jour sur l'appareil:
echo 1. Copier le nouveau OBB vers: /Android/obb/com.tsuki.battleroyale/
echo 2. Ou utiliser: UpdateOBBOnDevice.bat
echo.

:: Proposer mise à jour directe
where adb >nul 2>nul
if %ERRORLEVEL% equ 0 (
    set /p update_choice="Voulez-vous mettre à jour l'OBB sur l'appareil connecté ? [Y/N]: "
    if /i "%update_choice%"=="Y" (
        call UpdateOBBOnDevice.bat
    )
)

pause