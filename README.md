# Codex Limits Windows

Application Windows locale qui affiche le quota Codex restant, mesure le rythme de consommation et estime le moment où le quota atteindrait 0 % selon un planning de travail configurable.

![Aperçu de Codex Limits Windows](docs/codex-limits.png)

> [!IMPORTANT]
> **Projet indépendant et non officiel.** Codex Limits Windows n’est ni affilié, ni approuvé, ni soutenu par OpenAI. Les prévisions sont des estimations locales et ne garantissent ni la disponibilité future du service, ni l’exactitude des limites fournies par Codex.

## Installation — télécharger et lancer l’EXE

> [!TIP]
> **Le dépôt GitHub n’est pas nécessaire pour utiliser l’application.** Télécharge simplement l’EXE depuis la page **Releases**, puis double-clique dessus.

1. Ouvre la page **Releases** du dépôt.
2. Télécharge `CodexLimits.Windows-<version>-win-x64.exe`.
3. Lance l’EXE.

L’EXE est autonome :

- aucun runtime .NET n’est à installer ou à télécharger, car .NET 8 est inclus dans l’EXE ;
- aucun clone, `git pull`, SDK .NET ou script PowerShell n’est nécessaire pour l’utilisateur final ;
- si Codex CLI est déjà installé, il est réutilisé immédiatement, sans téléchargement, sans mise à jour et sans contrôle de connexion préalable ;
- si Codex CLI est absent, l’application propose de lancer l’installateur Windows officiel d’OpenAI ;
- après le démarrage, l’application peut rester active dans la zone de notification Windows.

L’installation de Codex CLI n’est lancée qu’après confirmation explicite et uniquement lorsqu’aucun exécutable Codex local n’est trouvé. Elle utilise :

```text
https://chatgpt.com/codex/install.ps1
```

## Compatibilité

- **Windows 10/11 x64 : pris en charge** via l’EXE autonome.
- **Windows ARM64 : non publié actuellement** ; une build dédiée peut être ajoutée plus tard.
- **Linux : non pris en charge actuellement.** L’interface utilise WPF, une technologie Windows. Un simple fichier `.sh` ne peut donc pas faire fonctionner cette version sous Linux. Une version Linux nécessiterait un port de l’interface vers une technologie multiplateforme, par exemple Avalonia, puis la création d’un binaire Linux accompagné éventuellement d’un script `.sh`.


## Fonctionnalités

- lecture du quota via l’interface locale documentée de `codex app-server` ;
- pourcentage restant et consommé ;
- cible journalière, consommation réelle, projection actuelle et historique ;
- estimation du jour et de l’heure où le quota atteindrait 0 % au rythme actuel ;
- quota encore utilisable aujourd’hui pour rester sur la cible ;
- planning configurable : jours, horaires et fréquence d’actualisation ;
- réserve de sécurité configurable ;
- historique local limité à 90 jours ;
- français et anglais ;
- fonctionnement en arrière-plan dans la zone de notification Windows ;
- icône et infobulle avec quota total restant et quota encore utilisable aujourd’hui.

## Fonctionnement en arrière-plan

Fermer, réduire ou masquer la fenêtre ne termine pas l’application. Au premier passage en arrière-plan, un message explique ce comportement.

- double-clique sur l’icône de la zone de notification pour rouvrir la fenêtre ;
- clic droit → **Quitter** pour arrêter complètement le processus.

Le lancement automatique avec Windows n’est pas activé par défaut.

## Confidentialité

Codex Limits Windows n’intègre aucune télémétrie, publicité ou analyse distante. L’application ne lit ni les prompts, ni les conversations, ni les fichiers de travail Codex.

Les données locales sont stockées dans :

```text
%LOCALAPPDATA%\CodexLimitsWindows\
├── settings.json
├── ui-state.json
└── History\*.jsonl
```

L’historique contient uniquement l’heure d’observation, le pourcentage restant et l’heure de reset. Consulte [PRIVACY.md](PRIVACY.md) pour le détail des communications réseau liées à l’installation optionnelle de Codex CLI.

## Utiliser les sources — développeurs uniquement

Cette section concerne uniquement les personnes qui souhaitent modifier, compiler ou contribuer au projet. Pour utiliser normalement l’application, télécharge simplement l’EXE depuis **Releases** : le dépôt n’est pas nécessaire.

Le dépôt conserve uniquement le code source et les fichiers nécessaires au projet. L’exécutable généré est publié dans GitHub Releases et n’est pas versionné dans Git.

Prérequis développeur :

- Windows 10 ou Windows 11 x64 ;
- SDK .NET 8 ;
- Codex CLI pour tester la lecture réelle du quota.

### Lancer depuis les sources

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

### Compiler et tester

```powershell
dotnet build .\CodexLimits.Windows.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

### Générer l’EXE autonome

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-exe.ps1
```

Résultats créés dans `artifacts\release` :

```text
CodexLimits.Windows-<version>-win-x64.exe
CodexLimits.Windows-<version>-win-x64.exe.sha256
CodexLimits.Windows-<version>-win-x64.zip
CodexLimits.Windows-<version>-win-x64.zip.sha256
```

Le ZIP contient l’EXE et les documents légaux. L’EXE lui-même est autonome et inclut le runtime .NET 8.

## CI/CD et publication

La CI GitHub Actions :

1. vérifie que le dépôt ne contient pas de sauvegarde, secret ou fichier généré suivi par Git ;
2. compile la solution ;
3. exécute les smoke tests ;
4. produit l’EXE autonome Windows x64 ;
5. publie les fichiers comme artefact de workflow ;
6. crée ou met à jour automatiquement une GitHub Release lorsqu’un tag `v*` est poussé.

Exemple de publication :

```powershell
git tag v0.6.4
git push origin v0.6.4
```

L’exécutable n’est pas signé par défaut. Une signature Authenticode est recommandée avant une distribution publique afin de réduire les avertissements SmartScreen.

## Fonctionnement technique

L’application lance localement :

```text
codex app-server --listen stdio://
```

puis lit `account/rateLimits/read`. L’application ne contourne pas les quotas et ne manipule pas les identifiants Codex.

## Limites connues

- les formats renvoyés par le Codex CLI peuvent évoluer ;
- les prévisions deviennent plus fiables après plusieurs échantillons locaux ;
- une consommation très irrégulière peut rendre la projection moins représentative ;
- Windows peut afficher un avertissement SmartScreen pour un exécutable non signé ;
- l’installation optionnelle de Codex CLI, uniquement lorsqu’il est absent, nécessite une connexion Internet et Windows PowerShell ;
- cette version graphique ne fonctionne pas nativement sous Linux, car elle utilise WPF ;
- le nom du projet conserve le terme « Codex » à titre descriptif. Consulte [TRADEMARKS.md](TRADEMARKS.md).

## Structure

```text
src/
├── CodexLimits.App/       Interface WPF, installation Codex si absent, paramètres, icône système
└── CodexLimits.Core/      Lecture du quota, planning, prévisions, historique

tests/
└── CodexLimits.SmokeTests/

docs/
├── icon.png
├── icon.ico
└── codex-limits.png
```

## Sécurité

Ne publie jamais de sortie brute Codex, de capture contenant des informations de compte, de jeton ou de fichier local dans une issue publique. Consulte [SECURITY.md](SECURITY.md).

## Crédits

Ce port Windows reprend des idées et une partie de la logique de prévision du projet macOS [`thrr87/codex-limits`](https://github.com/thrr87/codex-limits), distribué sous licence MIT.

L’avis de licence d’origine est conservé dans [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Licence

Codex Limits Windows est distribué sous licence MIT. Voir [LICENSE](LICENSE).
