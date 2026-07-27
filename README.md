# Codex Limits Windows — prototype local v0.2

Port Windows expérimental de l’interface et du moteur de prévision de
[`thrr87/codex-limits`](https://github.com/thrr87/codex-limits).

Fonctions principales :

- quota Codex restant via le Codex CLI local ;
- courbes **Target / Actual / Current / Historical** ;
- projection jusqu’au reset et estimation d’épuisement ;
- rythme conseillé par heure active ou par jour travaillé ;
- planning configurable par jours et horaires ;
- week-ends et périodes inactives exclus des calculs ;
- actualisation automatique configurable, 30 minutes par défaut ;
- historique JSONL conservé uniquement en local ;
- icône dans la zone de notification.

## Planning par défaut

La version 0.2 démarre avec :

```text
Lundi à vendredi
09:00 à 18:00
Actualisation toutes les 30 minutes
```

Le bouton **⚙** ouvre une fenêtre permettant de modifier les jours, les heures
et la fréquence. En dehors du planning, les projections ne font pas avancer le
temps de consommation et aucune actualisation automatique n’est lancée. Le
bouton d’actualisation manuelle reste utilisable.

## Prérequis

1. Windows 10 ou 11 x64.
2. SDK .NET 8.
3. Codex CLI installé et connecté.
4. La commande `codex` doit fonctionner dans PowerShell.

```powershell
codex --version
dotnet --info
```

## Compilation

```powershell
dotnet build .\CodexLimits.Windows.sln --configuration Debug
```

## Mode démo

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-demo.ps1
```

## Données Codex réelles

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

Le programme lance localement :

```text
codex app-server --listen stdio://
```

Puis demande uniquement :

```text
account/rateLimits/read
```

Aucun identifiant, prompt, réponse ou fichier de code n’est lu ou enregistré.

## Tests locaux

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

## Données locales

Historique du quota :

```text
%LOCALAPPDATA%\CodexLimitsWindows\History\
```

Planning et fréquence :

```text
%LOCALAPPDATA%\CodexLimitsWindows\settings.json
```

Les échantillons d’historique contiennent seulement la date d’observation, le
pourcentage restant et la date du reset.

## Publication future en `.exe`

Après validation du build, du mode démo et des données réelles :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-exe.ps1
```

Le résultat est écrit dans `artifacts\win-x64\`.

## Licence et attribution

MIT. Voir `LICENSE` et `THIRD_PARTY_NOTICES.md`. Les parties adaptées de
`thrr87/codex-limits` conservent leur attribution.
