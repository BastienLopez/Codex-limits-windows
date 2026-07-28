# Checklist de publication

## Code et tests

- [ ] `git status` ne contient aucun `bin`, `obj`, `.bak`, secret ou fichier local.
- [ ] `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1` réussit.
- [ ] L’application réelle lit le quota avec une version supportée du Codex CLI.
- [ ] Le mode démonstration démarre sans compte Codex.
- [ ] La croix, la réduction et **Masquer** laissent l’application active en arrière-plan.
- [ ] Le message d’arrière-plan ne s’affiche qu’une seule fois.
- [ ] **Quitter** termine réellement le processus.
- [ ] Français et anglais ont été testés.

## Données et confidentialité

- [ ] `PRIVACY.md` correspond exactement aux données réellement stockées.
- [ ] Aucune télémétrie, URL de suivi ou dépendance analytique n’a été ajoutée.
- [ ] Les captures ne révèlent aucune information de compte.
- [ ] Une URL publique vers la politique de confidentialité est disponible avant une soumission au Store.

## Licence et attribution

- [ ] `LICENSE` est inclus dans l’archive.
- [ ] `THIRD_PARTY_NOTICES.md` est inclus sans modification de l’avis MIT d’origine.
- [ ] La mention « projet indépendant et non officiel » est visible dans le README et l’application.
- [ ] Les directives de marque OpenAI ont été relues avant publication.

## Distribution Windows

- [ ] L’archive a été créée avec `scripts\publish-exe.ps1`.
- [ ] Le SHA-256 a été publié avec l’archive.
- [ ] L’exécutable ou l’installateur a été signé avec un certificat Authenticode reconnu, ou la distribution passe par un canal qui signe le package.
- [ ] La signature a été contrôlée avec `Get-AuthenticodeSignature`.
- [ ] L’exécutable a été testé sur une machine propre Windows 10/11.
- [ ] Les avertissements SmartScreen et antivirus ont été évalués.
- [ ] Le prérequis Codex CLI est indiqué avant le téléchargement.

## Publication GitHub

- [ ] La release contient le ZIP, son SHA-256, les notes de version et les limitations connues.
- [ ] Le code source de la version publiée est tagué.
- [ ] Le signalement privé des vulnérabilités est activé dans les paramètres GitHub.
- [ ] Aucun historique Git, token ou certificat privé n’est inclus dans le ZIP de distribution.

## Références officielles

- OpenAI — directives de marque : https://openai.com/brand/
- OpenAI Codex — documentation `app-server` : https://github.com/openai/codex/blob/main/codex-rs/app-server/README.md
- Microsoft Store — politiques : https://learn.microsoft.com/windows/apps/publish/store-policies
- Microsoft — options de signature : https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options
