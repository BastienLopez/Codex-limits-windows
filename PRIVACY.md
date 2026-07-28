# Politique de confidentialité — Codex Limits Windows

Dernière mise à jour : 28 juillet 2026

## Résumé

Codex Limits Windows fonctionne principalement en local et n’intègre aucune télémétrie, publicité, analyse d’audience ou compte propre à l’application.

## Données consultées

L’application lance le Codex CLI installé sur l’ordinateur et lit les informations de limite d’utilisation exposées par `codex app-server`, notamment :

- pourcentage utilisé ou restant ;
- durée de la fenêtre de quota ;
- date et heure de reset ;
- éventuelles limites supplémentaires fournies par Codex.

L’application ne cherche pas à lire les prompts, conversations, fichiers de travail, clés API, cookies ou jetons d’authentification.

## Données stockées localement

Les fichiers suivants peuvent être créés dans `%LOCALAPPDATA%\CodexLimitsWindows` :

- `settings.json` : planning, langue, fréquence d’actualisation et réserve de sécurité ;
- `ui-state.json` : état d’affichage du message informant du fonctionnement en arrière-plan ;
- `History\YYYY-MM-DD.jsonl` : heure d’observation, pourcentage restant et heure de reset.

L’historique est limité à 90 jours. Les fichiers quotidiens plus anciens sont supprimés lors du chargement de l’historique.

## Communications réseau

L’application n’envoie aucune télémétrie.

L’EXE autonome contient déjà le runtime .NET 8 : l’application ne télécharge et n’installe donc jamais .NET sur l’ordinateur de l’utilisateur.

Au démarrage, l’application cherche uniquement si un exécutable Codex CLI est déjà présent dans le `PATH` ou dans ses emplacements d’installation Windows connus. Lorsqu’il existe, il est réutilisé tel quel : aucun téléchargement, aucune mise à jour, aucun appel à `codex --version` et aucun contrôle préalable avec `codex login status` ne sont effectués.

Après confirmation explicite de l’utilisateur, Codex Limits Windows peut lancer Windows PowerShell pour récupérer et exécuter l’installateur Windows officiel publié à l’adresse :

`https://chatgpt.com/codex/install.ps1`

Cet installateur télécharge Codex CLI depuis les sources de distribution officielles utilisées par OpenAI. L’application ne transmet aucune donnée de quota à ce script.

Après installation, le Codex CLI peut communiquer avec les services OpenAI dans le cadre de son fonctionnement normal, de l’authentification et de la lecture des limites de compte, conformément aux conditions et politiques d’OpenAI.

## Partage de données

Aucune donnée locale n’est vendue, louée ou transmise par Codex Limits Windows. Tout partage manuel d’une capture, d’un historique ou d’une sortie Codex relève de l’action de l’utilisateur.

## Conservation et suppression

Pour supprimer toutes les données de l’application :

1. quitter complètement Codex Limits Windows depuis l’icône de la zone de notification ;
2. supprimer le dossier `%LOCALAPPDATA%\CodexLimitsWindows`.

La suppression de l’exécutable ne supprime pas automatiquement ce dossier ni les données propres au Codex CLI situées dans le profil utilisateur.

## Sécurité

Les fichiers locaux héritent des protections du compte Windows et du système de fichiers. Codex Limits Windows ne chiffre pas séparément son historique, car celui-ci ne contient pas de prompt, de conversation ou de secret d’authentification.

## Enfants

L’application n’est pas destinée spécifiquement aux enfants et ne collecte volontairement aucune donnée personnelle d’enfant.

## Modifications

Cette politique peut être mise à jour en cas de changement fonctionnel. La date en haut du document indique la version applicable.

## Contact

Pour une question de confidentialité, ouvre une discussion ou une issue dans le dépôt du projet sans joindre de sortie brute Codex ni d’information de compte. Pour un problème sensible, utilise le canal décrit dans [SECURITY.md](SECURITY.md).

---

# Privacy policy — English summary

Codex Limits Windows sends no telemetry. It stores only local settings, UI state, and quota samples. The self-contained EXE does not download .NET. With explicit user consent, it may invoke OpenAI’s official Windows Codex installer from `https://chatgpt.com/codex/install.ps1` only when no local Codex CLI executable is found. Existing installations are never updated or login-checked by the application. Normal Codex authentication and service communication are handled by the Codex CLI.
