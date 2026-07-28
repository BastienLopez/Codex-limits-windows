# Codex Limits Windows

Codex Limits Windows est une application WPF locale qui affiche le quota Codex restant, son rythme de consommation et une projection adaptée à un planning de travail configurable.

![Aperçu de Codex Limits Windows](docs/codex-limits.png)

## Fonctionnalités

- lecture du quota réel via `codex app-server` ;
- pourcentage restant et consommé ;
- objectif journalier, consommation réelle, projection actuelle et historique ;
- estimation du quota restant à la fin du planning ;
- quota encore utilisable aujourd’hui pour rester sur la cible ;
- planning configurable : jours, horaires et fréquence d’actualisation ;
- réserve de sécurité configurable ;
- historique enregistré localement ;
- fonctionnement en zone de notification Windows ;
- icône agrandie dans la barre des tâches et la zone de notification ;
- info-bulle de l’icône avec le quota total restant et le quota encore utilisable aujourd’hui ;
- interface disponible en français et en anglais.

## Confidentialité

L’application fonctionne localement :

- elle ne lit pas les conversations ou les prompts Codex ;
- elle n’envoie aucune télémétrie ;
- elle n’enregistre que l’historique nécessaire au calcul de consommation ;
- elle utilise la session Codex déjà ouverte sur la machine.

## Prérequis

- Windows 10 ou Windows 11 ;
- SDK .NET 8 pour compiler le projet ;
- Codex CLI installé et connecté.

## Lancer l’application

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

## Lancer directement en arrière-plan

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-background.ps1
```

La réduction, le bouton **Masquer** et la fermeture de la fenêtre cachent l’interface sans arrêter l’application. Le suivi et les actualisations continuent depuis la zone de notification. Double-clique sur l’icône pour rouvrir la fenêtre et utilise **Quitter** dans son menu pour arrêter réellement le processus.

## Compiler

```powershell
dotnet build .\CodexLimits.Windows.sln --configuration Debug
```

Le build génère automatiquement `docs/icon.ico` à partir de `docs/icon.png`. Cette icône est utilisée pour l’exécutable, les fenêtres Windows et la zone de notification.

## Publier un exécutable autonome

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-exe.ps1
```

La publication est créée dans :

```text
artifacts\win-x64
```

## Structure principale

```text
src/
├── CodexLimits.App/       Interface WPF, paramètres et icône système
└── CodexLimits.Core/      Calculs de planning, prévisions et historique

tests/
└── CodexLimits.SmokeTests/

docs/
├── icon.png               Icône source du projet
├── icon.ico               Icône Windows générée automatiquement
└── codex-limits.png       Capture d’écran utilisée dans ce README
```

## Crédits

Ce projet Windows est inspiré du projet open source [`thrr87/codex-limits`](https://github.com/thrr87/codex-limits).

## Licence

Distribué sous licence MIT. Voir [LICENSE](LICENSE).
