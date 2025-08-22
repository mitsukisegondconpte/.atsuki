@echo off
echo =====================================================
echo    Tsuki BR Demo - Installation sur Appareil
echo =====================================================

:: Configuration
set BUILD_PATH=%~dp0
set APK_NAME=TsukiBRDemo.apk
set OBB_NAME=main.1.com.tsuki.battleroyale.obb
set PACKAGE_NAME=com.tsuki.battleroyale
set OBB_DEVICE_PATH=/sdcard/Android/obb/%PACKAGE_NAME%/

echo.
echo Vérification des fichiers...

:: Vérifier APK
if not exist "%BUILD_PATH%%APK_NAME%" (
    echo ERREUR: APK non trouvé: %APK_NAME%
    echo Lancez d'abord BuildAndroid.bat
    pause
    exit /b 1
)
echo ✓ APK trouvé: %APK_NAME%

:: Vérifier OBB
if not exist "%BUILD_PATH%%OBB_NAME%" (
    echo ERREUR: OBB non trouvé: %OBB_NAME%
    echo Lancez d'abord BuildAndroid.bat
    pause
    exit /b 1
)
echo ✓ OBB trouvé: %OBB_NAME%

:: Vérifier ADB
where adb >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo ERREUR: ADB non trouvé dans le PATH!
    echo Assurez-vous que Android SDK est installé et configuré.
    pause
    exit /b 1
)
echo ✓ ADB trouvé

echo.
echo =====================================================
echo          Vérification de l'appareil Android
echo =====================================================

:: Vérifier la connexion de l'appareil
adb devices | findstr /r "device$" >nul
if %ERRORLEVEL% neq 0 (
    echo ERREUR: Aucun appareil Android détecté!
    echo.
    echo Solutions:
    echo 1. Connecter votre appareil via USB
    echo 2. Activer le débogage USB
    echo 3. Autoriser l'ordinateur sur l'appareil
    echo 4. Essayer: adb devices
    echo.
    adb devices
    pause
    exit /b 1
)

echo ✓ Appareil Android détecté
adb devices

echo.
echo =====================================================
echo            Installation de l'APK
echo =====================================================

:: Désinstaller ancienne version si elle existe
echo Désinstallation de l'ancienne version...
adb uninstall %PACKAGE_NAME% >nul 2>&1

:: Installer nouvelle APK
echo Installation de l'APK...
adb install "%BUILD_PATH%%APK_NAME%"

if %ERRORLEVEL% neq 0 (
    echo ERREUR: Échec de l'installation de l'APK!
    echo.
    echo Solutions possibles:
    echo 1. Activer "Sources inconnues" dans les paramètres Android
    echo 2. Vérifier l'espace de stockage disponible
    echo 3. Redémarrer l'appareil
    pause
    exit /b 1
)

echo ✓ APK installé avec succès

echo.
echo =====================================================
echo             Installation de l'OBB
echo =====================================================

:: Créer le dossier OBB
echo Création du dossier OBB...
adb shell mkdir -p "%OBB_DEVICE_PATH%"

:: Copier le fichier OBB
echo Copie du fichier OBB (cela peut prendre du temps)...
adb push "%BUILD_PATH%%OBB_NAME%" "%OBB_DEVICE_PATH%"

if %ERRORLEVEL% neq 0 (
    echo ERREUR: Échec de la copie de l'OBB!
    pause
    exit /b 1
)

echo ✓ OBB copié avec succès

echo.
echo =====================================================
echo              Vérification finale
echo =====================================================

:: Vérifier que l'APK est installé
adb shell pm list packages | findstr %PACKAGE_NAME% >nul
if %ERRORLEVEL% equ 0 (
    echo ✓ Package installé et reconnu par le système
) else (
    echo ⚠ Avertissement: Package non détecté par le système
)

:: Vérifier que l'OBB est bien copié
adb shell ls "%OBB_DEVICE_PATH%%OBB_NAME%" >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo ✓ Fichier OBB présent sur l'appareil
) else (
    echo ⚠ Avertissement: Fichier OBB non trouvé
)

:: Obtenir des infos sur l'OBB
echo.
echo Informations sur l'OBB:
adb shell ls -lh "%OBB_DEVICE_PATH%%OBB_NAME%" 2>nul

echo.
echo =====================================================
echo               Installation Terminée!
echo =====================================================

echo.
echo Installation réussie sur l'appareil Android!
echo.
echo Application: Tsuki BR Demo
echo Package: %PACKAGE_NAME%
echo.
echo Vous pouvez maintenant:
echo 1. Déconnecter l'appareil
echo 2. Lancer l'application depuis le menu
echo 3. Autoriser les permissions si demandées
echo.

:: Proposer de lancer l'app
set /p launch_choice="Voulez-vous lancer l'application maintenant ? [Y/N]: "
if /i "%launch_choice%"=="Y" (
    echo Lancement de l'application...
    adb shell am start -n %PACKAGE_NAME%/com.unity3d.player.UnityPlayerActivity
    if %ERRORLEVEL% equ 0 (
        echo ✓ Application lancée
    ) else (
        echo ⚠ Échec du lancement automatique
        echo Lancez manuellement depuis le menu de l'appareil
    )
)

echo.
echo Appuyez sur une touche pour fermer...
pause >nul