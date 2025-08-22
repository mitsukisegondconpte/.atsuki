@echo off
echo =====================================================
echo     Tsuki BR Demo - Android Build Script
echo =====================================================

:: Configuration
set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\2021.3.15f1\Editor\Unity.exe"
set PROJECT_PATH=%~dp0..
set BUILD_PATH=%~dp0
set APK_NAME=TsukiBRDemo.apk
set OBB_NAME=main.1.com.tsuki.battleroyale.obb

echo.
echo Configuration:
echo - Unity Path: %UNITY_PATH%
echo - Project Path: %PROJECT_PATH%
echo - Build Path: %BUILD_PATH%
echo.

:: Vérifier si Unity existe
if not exist %UNITY_PATH% (
    echo ERREUR: Unity n'est pas trouvé dans le chemin spécifié!
    echo Veuillez modifier UNITY_PATH dans ce script.
    pause
    exit /b 1
)

:: Créer le dossier build s'il n'existe pas
if not exist "%BUILD_PATH%" mkdir "%BUILD_PATH%"

echo =====================================================
echo           Étape 1: Build APK avec OBB
echo =====================================================

:: Lancer Unity en mode batch pour build
%UNITY_PATH% -batchmode -quit ^
    -projectPath "%PROJECT_PATH%" ^
    -executeMethod AndroidBuilder.BuildAPKWithOBB ^
    -buildPath "%BUILD_PATH%" ^
    -logFile "%BUILD_PATH%build_log.txt"

if %ERRORLEVEL% neq 0 (
    echo ERREUR: Échec du build Unity!
    echo Vérifiez le log: %BUILD_PATH%build_log.txt
    pause
    exit /b 1
)

echo.
echo =====================================================
echo      Étape 2: Vérification des fichiers générés
echo =====================================================

:: Vérifier APK
if exist "%BUILD_PATH%%APK_NAME%" (
    echo ✓ APK généré: %APK_NAME%
    for %%A in ("%BUILD_PATH%%APK_NAME%") do echo   Taille: %%~zA octets
) else (
    echo ✗ ERREUR: APK non généré!
    pause
    exit /b 1
)

:: Vérifier OBB
if exist "%BUILD_PATH%%OBB_NAME%" (
    echo ✓ OBB généré: %OBB_NAME%
    for %%A in ("%BUILD_PATH%%OBB_NAME%") do echo   Taille: %%~zA octets
) else (
    echo ✗ ERREUR: OBB non généré!
    pause
    exit /b 1
)

echo.
echo =====================================================
echo              Étape 3: Tests de validation
echo =====================================================

:: Test de l'APK (basique)
echo Vérification de l'intégrité de l'APK...
if exist "%ANDROID_HOME%\build-tools\33.0.0\aapt.exe" (
    "%ANDROID_HOME%\build-tools\33.0.0\aapt.exe" dump badging "%BUILD_PATH%%APK_NAME%" > nul 2>&1
    if %ERRORLEVEL% equ 0 (
        echo ✓ APK valide
    ) else (
        echo ⚠ Avertissement: APK potentiellement corrompu
    )
) else (
    echo ⚠ Android Build Tools non trouvé, impossible de valider l'APK
)

echo.
echo =====================================================
echo                    Build Terminé!
echo =====================================================

echo.
echo Fichiers générés:
echo - %BUILD_PATH%%APK_NAME%
echo - %BUILD_PATH%%OBB_NAME%
echo.
echo Pour installer:
echo 1. Copier l'APK sur votre appareil Android
echo 2. Copier l'OBB vers: /Android/obb/com.tsuki.battleroyale/
echo 3. Ou utiliser InstallToDevice.bat
echo.

:: Proposer installation directe si ADB disponible
where adb >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo ADB détecté. Voulez-vous installer directement sur l'appareil connecté ?
    set /p install_choice="[Y/N]: "
    if /i "%install_choice%"=="Y" (
        call InstallToDevice.bat
    )
)

echo.
echo Appuyez sur une touche pour fermer...
pause >nul