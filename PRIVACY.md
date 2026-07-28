# Politique de confidentialité — Codex Limits Windows

Dernière mise à jour : 28 juillet 2026

## Résumé

Codex Limits Windows fonctionne localement et n’intègre aucune télémétrie, publicité, outil d’analyse, service de suivi ou compte propre à l’application.

## Données consultées

L’application lance le Codex CLI déjà installé sur l’ordinateur et lit les informations de limite d’utilisation exposées par `codex app-server`, notamment :

- pourcentage utilisé ou restant ;
- durée de la fenêtre de quota ;
- date et heure de reset ;
- éventuelles limites supplémentaires fournies par Codex.

L’application ne cherche pas à lire :

- les prompts ;
- les conversations ;
- le contenu des fichiers ;
- les clés API, cookies ou jetons d’authentification ;
- l’identité complète du compte.

## Données stockées localement

Les fichiers suivants peuvent être créés dans `%LOCALAPPDATA%\CodexLimitsWindows` :

- `settings.json` : planning, langue, fréquence d’actualisation et réserve de sécurité ;
- `ui-state.json` : état d’affichage du message informant du fonctionnement en arrière-plan ;
- `History\YYYY-MM-DD.jsonl` : heure d’observation, pourcentage restant et heure de reset.

L’historique est limité à 90 jours. Les fichiers quotidiens plus anciens sont supprimés lors du chargement de l’historique. L’application n’envoie pas ces fichiers à un serveur tiers.

## Communications réseau

Codex Limits Windows ne contient pas de client réseau direct vers OpenAI. Le Codex CLI peut communiquer avec les services OpenAI dans le cadre de son fonctionnement normal et conformément à ses propres conditions et politiques.

## Partage de données

Aucune donnée locale n’est vendue, louée ou transmise par Codex Limits Windows. Si l’utilisateur copie lui-même le dossier local, une capture d’écran ou une sortie Codex vers un service tiers, ce partage relève de son action.

## Conservation et suppression

Pour supprimer toutes les données de l’application :

1. quitter complètement Codex Limits Windows depuis l’icône de la zone de notification ;
2. supprimer le dossier `%LOCALAPPDATA%\CodexLimitsWindows`.

La désinstallation ou la suppression de l’exécutable peut ne pas supprimer automatiquement ce dossier local.

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

Codex Limits Windows is a local, unofficial application. It sends no telemetry and stores only settings, a one-time background-notice flag, and local quota samples containing timestamps, remaining percentages, and reset times. It does not read prompts, conversations, files, or credentials. Delete `%LOCALAPPDATA%\CodexLimitsWindows` after quitting the app to remove its local data.
