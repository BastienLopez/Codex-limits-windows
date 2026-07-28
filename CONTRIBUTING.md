# Contribuer à Codex Limits Windows

Merci de contribuer au projet.

## Avant de proposer une modification

1. vérifie que le problème est reproductible avec la dernière version du Codex CLI ;
2. ne joins jamais de jeton, sortie brute `app-server`, capture de compte ou historique local non anonymisé ;
3. garde les modifications ciblées et explique leur impact sur le calcul, l’interface ou la confidentialité.

## Développement

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Avant une pull request :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\clean.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\audit-release.ps1
```

## Style

- C# nullable activé ;
- avertissements traités comme erreurs ;
- fichiers texte en UTF-8 ;
- scripts PowerShell compatibles avec Windows PowerShell 5.1 ;
- aucune télémétrie ou dépendance réseau sans discussion préalable ;
- toute nouvelle donnée locale doit être documentée dans `PRIVACY.md`.

## Tests attendus

Une correction de calcul doit inclure un scénario synthétique dans `tests/CodexLimits.SmokeTests/Program.cs`. Une modification visuelle doit être testée en français et en anglais avec la mise à l’échelle Windows habituelle.

## Licence

En contribuant, tu acceptes que ta contribution soit distribuée sous la licence MIT du projet.
