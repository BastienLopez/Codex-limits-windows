# Audit de préparation à la publication — 0.5.0

Date : 28 juillet 2026

## Nettoyage effectué

- suppression du dossier `.git` dans l’archive distribuable ;
- suppression des dossiers `bin`, `obj`, `.vs`, `artifacts` et `TestResults` ;
- suppression des fichiers `.bak`, anciens patchs et composants de paramètres inutilisés ;
- ajout d’un `.gitignore`, d’un `.editorconfig` et d’un `.gitattributes` complets ;
- exclusion explicite des certificats et clés privées.

## Vérifications statiques effectuées

- XML/XAML/csproj/manifeste bien formés ;
- handlers XAML présents dans les fichiers code-behind ;
- contrôles nommés utilisés par le code présents dans le XAML ;
- absence de caractères UTF-8 corrompus détectables ;
- recherche ciblée de formats de secrets courants ;
- présence et lisibilité des images PNG/ICO ;
- cohérence de version `0.5.0` dans les métadonnées et le client `app-server`.

## Améliorations de publication

- message unique expliquant le fonctionnement en arrière-plan ;
- menu **À propos** avec disclaimer non officiel ;
- politique de confidentialité et politique de sécurité ;
- notice tierce contenant la licence MIT originale ;
- notice de marques ;
- pipeline CI Windows ;
- script de publication autonome avec archive ZIP et SHA-256 ;
- checklist de signature et de publication.

## Validation encore nécessaire sur Windows

L’environnement de préparation ne disposait pas du SDK .NET. Avant publication, exécuter sur Windows :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-exe.ps1
```

Tester ensuite l’exécutable publié sur une machine propre avec le Codex CLI installé.

## Points externes non automatisables

- signature Authenticode avec le certificat du véritable éditeur ;
- création d’une URL publique de politique de confidentialité pour un Store ;
- validation finale du nom au regard des directives de marque applicables ;
- contrôle SmartScreen et antivirus après signature et publication.
