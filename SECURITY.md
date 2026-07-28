# Politique de sécurité

## Versions prises en charge

Seule la dernière version publiée est maintenue. Les anciennes archives peuvent contenir des bugs déjà corrigés.

## Signaler une vulnérabilité

N’ouvre pas d’issue publique pour une vulnérabilité exploitable ou pour un rapport contenant des informations de compte.

Utilise en priorité la fonction **Report a vulnerability** / **Private vulnerability reporting** du dépôt GitHub, lorsqu’elle est activée. À défaut, crée un brouillon minimal sans données sensibles et demande un canal privé au mainteneur.

Inclure si possible :

- version de l’application et du Codex CLI ;
- version de Windows ;
- description de l’impact ;
- étapes de reproduction minimales ;
- correctif suggéré, le cas échéant.

Ne joins jamais :

- jeton, cookie, clé API ou fichier d’authentification ;
- sortie brute complète de `codex app-server` ;
- capture contenant l’identité du compte ;
- historique local non anonymisé.

## Périmètre

Les problèmes pertinents comprennent notamment :

- lecture ou exposition involontaire de secrets ;
- écriture de fichiers hors des emplacements prévus ;
- exécution de commande non contrôlée ;
- chargement de ressource distante non attendu ;
- persistance ou démarrage automatique sans consentement ;
- fuite de données dans les logs, erreurs ou rapports.

Les erreurs de projection sans impact de sécurité doivent être signalées comme bugs ordinaires.

## Dépendance Codex CLI

Codex Limits Windows démarre le Codex CLI local avec `app-server --listen stdio://`. Les vulnérabilités du CLI officiel doivent également être signalées au projet OpenAI Codex selon son propre processus de sécurité.

Si aucun exécutable Codex CLI local n’est trouvé, l’application peut, uniquement après confirmation explicite, exécuter l’installateur Windows officiel publié à `https://chatgpt.com/codex/install.ps1`. Une installation existante n’est ni mise à jour ni soumise à un contrôle de connexion par Codex Limits Windows. Toute modification de cette URL, suppression de la confirmation ou ajout d’une autre source de téléchargement doit être considérée comme une modification sensible et faire l’objet d’une revue de sécurité.
