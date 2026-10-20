# GardeAudio — guide français

GardeAudio rétablit la sortie audio précédente lorsque Windows bascule sur la sortie d'une manette **PS5 DualSense branchée en USB**. Le périphérique de la manette reste activé.

La version 0.1.0 est une première version de test pour Windows x64. L'interface est en français. Elle a été validée avec une DualSense USB sous Windows 11 ; les autres manettes et le Bluetooth ne sont pas encore validés.

## Télécharger et utiliser

1. Télécharger le **[ZIP autonome Windows x64](https://github.com/Nagaraaa/GardeAudio/releases/download/v0.1.0/GardeAudio-v0.1.0-windows-x64.zip)**. Il contient le runtime .NET : aucune installation séparée n'est nécessaire. Une [version légère](https://github.com/Nagaraaa/GardeAudio/releases/download/v0.1.0/GardeAudio-v0.1.0-windows-x64-framework-dependent.zip) existe si le runtime .NET Desktop 8 x64 est déjà installé.
2. Extraire le ZIP dans un dossier accessible en écriture puis lancer `GardeAudio.exe`, sans droits administrateur. Les exécutables ne sont pas signés.
3. Sélectionner le casque dans Windows avant de brancher la manette. Garder la protection cochée et choisir une sortie de secours dans la liste.
4. Brancher la DualSense en USB. La protection surveille les changements pendant deux secondes et corrige au maximum une bascule par rôle audio.
5. Cliquer sur **Réduire près de l’horloge** pour laisser l'application active. **Quitter** et la croix de la fenêtre arrêtent le programme. Aucun démarrage automatique avec Windows n'est installé.

Pour changer volontairement de sortie, la sélectionner dans l'application puis cliquer sur **Utiliser**. Ce choix annule toute correction en attente, y compris pour choisir la manette. La case de protection ou le menu de l'icône permettent aussi de mettre le programme en pause.

## Limites

- Windows ne précise pas la cause d'un changement. Choisir manuellement la manette dans les paramètres Windows pendant les deux secondes du branchement peut donc déclencher une correction. Utiliser **Utiliser** dans GardeAudio ou mettre la protection en pause dans ce cas. Hors de cette fenêtre, le choix reste libre.
- Le programme rétablit la sortie après notification : une courte coupure reste possible selon le PC. Les applications qui imposent directement une sortie audio ne sont pas reroutées.
- La détection repose sur un nom d'interface audio contenant `DualSense`. Une manette déjà présente au démarrage n'est pas considérée comme un nouveau branchement.
- Aucun pilote, firmware, volume, rapport HID ou choix de microphone n'est modifié. Les vibrations, gâchettes adaptatives et effets sonores propres aux jeux **n'ont pas été testés dans un jeu compatible**.
- L'interface COM Windows utilisée pour changer de sortie n'est pas documentée officiellement ; une mise à jour de Windows pourrait affecter le fonctionnement.

Les 25 scénarios de politique et les 11 contrôles d'intégration ont réussi. Lors des branchements USB physiques, le testeur a confirmé que le son était resté dans son casque sans coupure audible. Cela décrit le PC testé, sans garantir le même résultat sur tout matériel. Les [notes de validation](validation-v0.1.0.md) précisent ce qui a réellement été vérifié.

## Données locales et désinstallation

Le dossier `donnees/`, à côté de l'exécutable, contient les réglages, l'état et un journal avec les noms et identifiants des sorties. Rien n'est transmis par l'application. Relire et masquer les identifiants avant de partager un diagnostic sur GitHub.

Pour désinstaller : quitter l'application puis supprimer son dossier.

[README principal, compilation et diagnostics](../README.md) · [Signaler un problème](https://github.com/Nagaraaa/GardeAudio/issues) · [Licence MIT](../LICENSE)
