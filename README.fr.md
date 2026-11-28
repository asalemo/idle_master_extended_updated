# Idle Master Extended Updated

[English](README.md) · [Télécharger la RC Windows x64](https://github.com/asalemo/idle_master_extended_updated/releases/tag/v1.11.0-rc.8) · [Wiki du fork](https://github.com/asalemo/idle_master_extended_updated/wiki)

Ce fork Windows d’Idle Master Extended repère les jeux de ta bibliothèque Steam qui ont encore des cartes à distribuer. Il utilise le programme auxiliaire `steam-idle.exe` pour signaler une activité en jeu à Steam. Steam détermine le moment où les cartes sont distribuées.

[Tester le compteur de cartes de la RC8](https://github.com/asalemo/idle_master_extended_updated/wiki/Premiers-pas#tester-le-compteur-de-cartes-dans-la-rc8)

## Choisir sa connexion dans la RC8

Clique sur **Se connecter**, puis choisis **Se connecter avec Steam** ou **Saisir les informations de session**. La saisie manuelle accepte sessionid, steamLoginSecure et le cookie parental facultatif pour cette exécution. Les préférences sont conservées. Les diagnostics et les compteurs d’activité restent en mémoire. La connexion par navigateur utilise un profil privé temporaire que l’application tente de supprimer à la fermeture du navigateur. Un arrêt forcé peut laisser des fichiers temporaires.

## Guide et langue de l’interface

[Guide RC8 : se connecter à Steam, changer de langue et tester un seul jeu](https://github.com/asalemo/idle_master_extended_updated/wiki/Premiers-pas) · [Tester les deux modes de connexion et vérifier les données conservées après fermeture](https://github.com/asalemo/idle_master_extended_updated/wiki/Verifier-la-confidentialite-RC8)

Dans **Fichier → Réglages**, choisis **Langue de Windows** ou le nom d’une langue, puis redémarre l’application.
La connexion Steam, le test d’un jeu et les diagnostics sont disponibles en français, anglais, allemand, espagnol, italien et portugais (Portugal et Brésil). Pour les autres langues de l’interface, les textes qui restent à traduire s’affichent en anglais.

## Utilisation

1. Ouvre le client Steam sur Windows 64 bits et connecte-toi.
2. Après avoir extrait tout le ZIP, lance `IdleMasterExtended.exe`. Garde `steam-idle.exe`, les DLL et les dossiers `Languages` et `runtimes` à côté de lui.
3. Connecte Idle Master à Steam et choisis un mode dans les paramètres. Le mode « un seul jeu » convient si tu souhaites garder un seul titre actif à la fois.

Les versions publiées pour ce fork figurent sur la [page des versions](https://github.com/asalemo/idle_master_extended_updated/releases). Pour tester une modification présente uniquement sur la branche `develop`, compile cette branche.

## Suivi de la file dans la RC8

Ouvre **Fichier → File de jeux** pour voir les titres actifs, en attente et reportés, passer le jeu actif, suspendre sa rotation ou remettre un titre en tête. En traitement individuel, la fenêtre indique la dernière lecture confirmée et la progression vers la rotation. Les résultats de session comptent les baisses par AppID, les jeux terminés et les lectures en échec. Avec le tri par défaut, un jeu admissible peut démarrer pendant la lecture des autres pages. Un tri global par nombre de cartes attend la bibliothèque complète.

## Délai de rotation réglable dans la RC8

Dans **Fichier → Réglages**, règle **Changer si le compteur reste inchangé (min)** entre 1 et 1 440 minutes. Le délai par défaut est de 120 minutes. Pour un essai rapide, choisis 3 minutes et utilise la file automatique avec un jeu actif et au moins un autre jeu admissible. Diagnostics indique la durée observée lors du passage au jeu suivant. Tu peux ensuite rétablir 120 minutes.

## File automatique dans la RC8

Quand un seul jeu est actif et que les vérifications réussies indiquent le même nombre positif de cartes pendant le délai choisi (120 minutes par défaut), la file passe au jeu suivant lors de la vérification. Diagnostics indique le jeu, le compteur et la durée. Le jeu reporté reste dans la file. Si lui seul est admissible, il reste actif. Une variation du compteur, une lecture en échec ou une pause remet la durée à zéro.

[Vérifier le passage au jeu suivant et la durée dans Diagnostics](https://github.com/asalemo/idle_master_extended_updated/wiki/Verifier-la-rotation-RC8)

## Fonctionnement du mode rapide

- Si plusieurs jeux ont moins de deux heures enregistrées et qu’aucun jeu admissible n’a atteint ce seuil, le programme les lance en parallèle, dans la limite de 30. Il vérifie leur progression toutes les cinq minutes.
- Dès qu’un jeu admissible atteint deux heures, le programme le garde actif seul. S’il ne reste qu’un seul jeu admissible, il passe directement à ce jeu, même sous le seuil. Les deux heures sont un critère de sélection propre au programme.
- Le mode rapide conserve le jeu sélectionné entre les vérifications. Si Steam est temporairement inaccessible pendant une vérification individuelle, le programme garde le jeu actif et réessaie une minute plus tard.

## Nouveautés RC2

**Fichier → Tester un jeu** recherche les titres installés. Le démarrage du test suspend la file automatique. Arrête le test ou ferme sa fenêtre, puis reprends la file quand tu le souhaites. **Fichier → Temps mesurés** affiche les délais. Les erreurs répétées sont regroupées dans **Diagnostics** et le programme auxiliaire fonctionne sans fenêtre.

[Tester un seul jeu](https://github.com/asalemo/idle_master_extended_updated/wiki/Tester-un-seul-jeu) · [Lire les erreurs dans Diagnostics](https://github.com/asalemo/idle_master_extended_updated/wiki/Lire-les-erreurs-dans-Diagnostics)

## Diagnostic des fermetures

Si un programme auxiliaire se ferme de lui-même ou ne démarre pas, ouvre le menu **Diagnostics** qui apparaît dans la fenêtre principale. Chaque entrée indique l’heure, le jeu, son AppID et le motif disponible. Une initialisation Steam refusée, une bibliothèque native manquante et une fermeture avec un code inconnu ont des messages distincts. Les arrêts demandés par Idle Master (pause ou changement de jeu) ne sont pas signalés. Le menu conserve les 50 derniers problèmes distincts de la session.

Le statut « En jeu » attend la confirmation de l’initialisation Steam. Après un échec, le prochain essai a lieu une minute plus tard. **Fichier → Réessayer maintenant** relance immédiatement les jeux en attente. Une panne temporaire du site Steam conserve la session en cours.

## Changements de ce fork

La rotation qui lançait successivement chaque jeu pendant cinq secondes a été supprimée. La première page des badges est téléchargée une seule fois par lecture. Les bannières des jeux et la vérification de version se chargent en arrière-plan pour laisser l’application démarrer même si ces services sont indisponibles.

## Compiler sous Windows

Utilise Visual Studio avec la charge de travail **Développement Desktop .NET** et le pack de ciblage **.NET Framework 4.8**. Choisis **Release | Any CPU** pour les deux solutions.

1. Compile [steam-idle.sln](steam-idle%20Source/steam-idle.sln).
2. Ouvre [IdleMasterExtended.sln](Source/IdleMasterExtended.sln), restaure les dépendances NuGet et compile la solution.
3. Lance `Source\IdleMasterExtended\bin\Release\IdleMasterExtended.exe`. La compilation y copie le programme auxiliaire et les dépendances Steam.

## Origine et crédits

Ce projet reprend [Idle Master Extended de Jonas Nilson](https://github.com/jonas-med-ett-s/idle_master_extended), lui-même issu d’[Idle Master de jshackles](https://github.com/jshackles/idle_master), basé sur le code original de Stumpokapow. Le projet utilise Steamworks.NET et des icônes Open Iconic. Le [wiki historique](https://github.com/jonas-med-ett-s/idle_master_extended/wiki) décrit la configuration et les fonctions de la version d’origine. Voir la [licence](LICENSE).

[Comparer le badge Steam et tester la rotation en trois minutes (modèle de résultat)](https://github.com/asalemo/idle_master_extended_updated/wiki/Premiers-pas#comparer-le-badge-steam-et-vérifier-la-rotation-automatique-en-trois-minutes)
