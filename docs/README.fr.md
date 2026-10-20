# Guardio, guide en français

Quand on branche une manette en USB (DualSense, DualShock 4, Xbox…), Windows bascule souvent le son dessus. Guardio remet aussitôt la sortie audio précédente. La manette reste utilisable normalement.

<p align="center"><img src="images/guardio-light.png" alt="Fenêtre de Guardio" width="400"></p>

## Installation

1. Télécharger **[Guardio v0.3.0](https://github.com/Nagaraaa/Guardio/releases/download/v0.3.0/Guardio-v0.3.0-windows-x64.zip)**. Ce ZIP contient .NET : rien d'autre à installer.
2. L'extraire où on veut et lancer `Guardio.exe`, sans droits administrateur.

Pour désinstaller, désactiver **Lancer avec Windows**, quitter Guardio et supprimer son dossier.

Tu viens de GardeAudio ? Quitte-le, puis copie son dossier `donnees/` à côté de `Guardio.exe` pour garder tes réglages.

## Utilisation

- **L'interrupteur en haut** active ou met en pause la protection.
- **Sortie** : la sortie audio actuelle. Un choix fait ici est toujours respecté, même la manette.
- **Secours** : sortie utilisée si la précédente a disparu.
- **Manettes → Gérer** : choisir quelles sorties sont des manettes si la tienne n'est pas reconnue.
- **Lancer avec Windows** : Guardio démarre caché dans la zone de notification à l'ouverture de session.
- Fermer la fenêtre laisse Guardio actif dans la zone de notification. **Quitter** (ou clic droit sur l'icône) l'arrête.

## Bon à savoir

- La protection dure deux secondes après le branchement. Ensuite, Windows reste entièrement libre.
- Choisir la manette dans les paramètres Windows pendant ces deux secondes sera aussi annulé. Dans ce cas, passe par **Sortie** dans Guardio.
- Aucun pilote, volume ou micro n'est modifié, et rien n'est envoyé sur Internet. Les réglages et le journal sont dans le dossier `donnees/`.

[README principal](../README.md) · [Signaler un problème](https://github.com/Nagaraaa/Guardio/issues)
