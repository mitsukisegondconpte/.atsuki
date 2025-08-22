# Tsuki BR Demo - Battle Royale Unity Project

Un projet Unity Battle Royale 3D style Free Fire pour Android, avec support LAN/local pour 2-50 joueurs.

## 📋 Fonctionnalités

### Gameplay Core
- **Joueur** : marche, course, saut, glissade, tir, collecte de loot
- **Stats** : 100 HP, soins, système de loot, munitions, pas de respawn
- **Armes** : Pistolet, SMG, Fusil, Sniper, Grenades, Molotov, armes de mêlée
- **Carte** : Île tropicale 1km x 1km avec zones forêt, ville abandonnée, désert, collines
- **IA Bots** : Ajoutés automatiquement si < 50 joueurs, IA basique (patrouille, tir, loot)
- **Safe Zone** : Cercle qui rétrécit côté serveur, dégâts hors zone
- **Caméra** : Vue TPS (Third Person Shooter) derrière le joueur
- **UID Joueur** : Unique, stocké localement avec PlayerPrefs

### Network
- **Mirror Networking** : Serveur LAN pour relayer positions, tirs, dégâts, loot
- **Connexion** : Clients connectés via IP locale
- **Gestion Bots** : IA gérée côté serveur
- **Split-Screen** : Option 2 joueurs sur même device

### Plateforme
- **Cible** : Android APK + OBB expansion file
- **Orientation** : Mode paysage forcé
- **Compatibilité** : Android API 24+ (target API 33)
- **Package** : com.tsuki.battleroyale

## 🏗️ Structure du Projet

```
Assets/
├── Scenes/
│   └── Main.unity                      # Scène principale
├── Scripts/
│   ├── Player/
│   │   └── PlayerController.cs         # Contrôleur joueur
│   ├── Combat/
│   │   └── WeaponSystem.cs            # Système d'armes
│   ├── AI/
│   │   └── BotController.cs           # IA des bots
│   ├── Game/
│   │   ├── SafeZoneController.cs      # Contrôleur zone sécurisée
│   │   ├── LootSystem.cs              # Système de loot
│   │   └── GameManager.cs             # Gestionnaire de jeu
│   └── Net/
│       ├── ServerManager.cs           # Gestionnaire serveur
│       └── ClientManager.cs           # Gestionnaire client
├── Resources/
│   └── Configs/
│       └── GameConfig.json            # Configuration du jeu
├── Prefabs/
│   ├── Player.prefab                  # Préfabriqué joueur
│   ├── Bot.prefab                     # Préfabriqué bot
│   ├── Weapon_*.prefab                # Préfabriqués armes
│   ├── LootCrate.prefab               # Préfabriqué loot
│   └── SafeZone.prefab                # Préfabriqué zone sécurisée
├── Art/
│   ├── Models/                        # Modèles 3D
│   └── Textures/                      # Textures
├── Audio/                             # Fichiers audio
├── UI/
│   └── HUD.prefab                     # Interface utilisateur
└── Plugins/
    └── Mirror/                        # Mirror Networking

StreamingAssets/
└── obb/                               # Assets lourds pour OBB

build/                                 # Fichiers de build
├── TsukiBRDemo.apk                   # Fichier APK
└── main.1.com.tsuki.battleroyale.obb  # Fichier OBB
```

## 🚀 Installation et Configuration

### Prérequis
1. Unity 2021.3 LTS ou plus récent
2. Android Build Support installé
3. SDK Android configuré

### Configuration Unity
1. Ouvrir Unity Hub
2. Créer nouveau projet 3D
3. Copier tous les fichiers dans le projet
4. Importer Mirror Networking depuis Asset Store
5. Configurer Build Settings pour Android

### Configuration Android
1. **File → Build Settings → Android**
2. **Player Settings** :
   - Company Name: `Tsuki Games`
   - Product Name: `Tsuki BR Demo`
   - Package Name: `com.tsuki.battleroyale`
   - Version: `1.0`
   - Bundle Version Code: `1`
   - Minimum API Level: `24`
   - Target API Level: `33`
   - Scripting Backend: `IL2CPP`
   - Target Architectures: `ARM64`
3. **Publishing Settings** :
   - Split Application Binary: ✅ Activé
4. **XR Settings** :
   - Orientation: `Landscape Left` uniquement

## 🎮 Comment Jouer

### Mode Host (Serveur)
1. Lancer l'application
2. Appuyer sur "HOST GAME"
3. Partager votre IP locale aux autres joueurs
4. Le jeu démarre automatiquement quand des joueurs rejoignent

### Mode Client
1. Lancer l'application
2. Entrer l'IP du host
3. Appuyer sur "JOIN GAME"
4. Attendre le début de la partie

### Contrôles
- **WASD** : Déplacement
- **Shift** : Course
- **Espace** : Saut
- **Ctrl** : Glissade
- **Souris** : Regarder autour
- **Clic gauche** : Tirer
- **R** : Recharger
- **1-7** : Changer d'arme
- **Molette** : Changer d'arme

### Mode Split-Screen
1. Connecter une manette
2. Appuyer sur "Split Screen" dans les options
3. Joueur 1 : Clavier/Souris
4. Joueur 2 : Manette

## 🔧 Build Process

### Build APK + OBB
1. **Assets → Build APK with OBB** (script personnalisé)
2. Ou utiliser le script batch : `BuildAndroid.bat`
3. Les fichiers seront générés dans `/build/`

### Installation sur Device
1. Activer "Sources inconnues" sur Android
2. Installer l'APK : `adb install TsukiBRDemo.apk`
3. Copier l'OBB vers : `/Android/obb/com.tsuki.battleroyale/`
4. Ou utiliser le script : `InstallToDevice.bat`

## 📁 Assets dans OBB

Les assets suivants sont automatiquement placés dans l'OBB :
- Textures haute résolution
- Modèles 3D complexes
- Fichiers audio (musiques, effets)
- Terrains et environnements
- Animations lourdes

## 🎯 Configuration Réseau

### Port par défaut : 7777
### Configuration firewall :
```bash
# Windows
netsh advfirewall firewall add rule name="Tsuki BR" dir=in action=allow protocol=TCP localport=7777

# Android (si rooté)
iptables -A INPUT -p tcp --dport 7777 -j ACCEPT
```

### Trouver votre IP locale :
- Windows : `ipconfig`
- Android : Paramètres → Wi-Fi → Détails réseau

## 🐛 Debug et Logs

### Logs Unity
- Activer "Development Build" dans Build Settings
- Logs accessibles via `adb logcat -s Unity`

### Debug Network
- Activer "Show Logs" dans ServerManager
- Interface debug en jeu avec statistiques réseau

## 🔄 Mise à Jour OBB

Pour mettre à jour seulement les assets sans recompiler :
1. Modifier assets dans `/StreamingAssets/obb/`
2. Build OBB only : `BuildOBBOnly.bat`
3. Remplacer fichier OBB sur device

## 📊 Performance

### Recommandations
- **RAM** : 4GB minimum
- **Storage** : 2GB libre
- **Android** : 7.0+ (API 24)
- **GPU** : Adreno 530+ / Mali G71+

### Optimisations
- LOD activé sur modèles 3D
- Occlusion culling
- Texture compression ASTC
- Audio compression OGG Vorbis

## 🎨 Personnalisation

### Modifier les armes
Éditer `/Assets/Resources/Configs/GameConfig.json`

### Ajouter des maps
1. Créer terrain dans Unity
2. Ajouter spawn points
3. Configurer dans GameManager

### Modifier l'UI
Préfabs UI dans `/Assets/UI/`

## 🤝 Multijoueur LAN

### Découverte automatique
Le jeu peut détecter automatiquement les serveurs locaux via broadcast UDP.

### Connexion manuelle
Entrer IP directement : `192.168.1.XXX`

## 📄 Licences

- Unity Personal License
- Mirror Networking MIT License
- Assets audio/3D sous licence libre ou créés par l'équipe

## 🆘 Support

### Issues communes
1. **"Impossible de se connecter"** : Vérifier firewall et IP
2. **"Lag important"** : Réduire qualité graphique
3. **"Crash au lancement"** : Vérifier OBB installé correctement

### Contact
- GitHub Issues pour bugs
- Discord : TsukiGames#1234
- Email : support@tsukigames.com

---

**Version** : 1.0  
**Date** : 2025  
**Développé avec** : Unity 2021.3 LTS + Mirror Networking