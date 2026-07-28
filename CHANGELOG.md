# Changelog

Toutes les modifications notables de Codex Limits Windows sont documentées ici.

## 0.5.1 - 2026-07-28

### Fixed

- Added the explicit `System.IO` import required by the WPF temporary build project.
- Moved per-monitor DPI configuration from `app.manifest` to `ApplicationHighDpiMode` to satisfy the Windows Forms build analyzer.

## 0.5.0 — 2026-07-28

### Ajouté

- fonctionnement persistant dans la zone de notification ;
- message informatif unique lors du premier passage en arrière-plan ;
- menu **À propos** avec mention de projet indépendant et non officiel ;
- icône Windows personnalisée et infobulle de quota ;
- légende du graphique représentée avec les styles réels des courbes ;
- politique de confidentialité, politique de sécurité, notice de marques et checklist de publication ;
- script d’audit de l’arborescence et recherche ciblée de secrets ;
- création automatisée d’une archive autonome avec SHA-256.

### Corrigé

- calcul du rythme sur les journées réellement travaillées ;
- projection journalière en escalier jusqu’à la fin du planning ;
- affichage UTF-8 français ;
- mise en page des paramètres et du graphique ;
- rendu sombre neutre `#101010` ;
- taille et rendu des icônes Windows.

### Sécurité et maintenance

- suppression des anciens fichiers de sauvegarde et composants inutilisés ;
- exclusion des certificats et clés privées dans `.gitignore` ;
- conservation de l’avis MIT du projet d’origine.
