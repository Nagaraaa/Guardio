# Guardio, guide en français

Quand on branche une manette en USB (DualSense, DualShock 4, Xbox…), Windows bascule souvent le son dessus. Guardio remet aussitôt la sortie audio précédente. La manette reste utilisable normalement.

## Installation

1. Télécharger le ZIP sur la [page des versions](https://github.com/Nagaraaa/Guardio/releases/latest). La version **standalone** contient .NET : rien d'autre à installer.
2. L'extraire où on veut et lancer `Guardio.exe`, sans droits administrateur.

Pour désinstaller, quitter Guardio et supprimer son dossier.

## Utilisation

- **L'interrupteur en haut** active ou met en pause la protection.
- **Sortie** : la sortie audio actuelle. Un choix fait ici est toujours respecté, même la manette.
- **Secours** : sortie utilisée si la précédente a disparu.
- **Manettes → Gérer** : choisir quelles sorties sont des manettes si la tienne n'est pas reconnue.
- **Réduire** : Guardio continue dans la zone de notification.

## Bon à savoir

- La protection dure deux secondes après le branchement. Ensuite, Windows reste entièrement libre.
- Choisir la manette dans les paramètres Windows pendant ces deux secondes sera aussi annulé. Dans ce cas, passe par **Sortie** dans Guardio.
- Aucun pilote, volume ou micro n'est modifié, et rien n'est envoyé sur Internet. Les réglages et le journal sont dans le dossier `donnees/`.

[README principal](../README.md) · [Signaler un problème](https://github.com/Nagaraaa/Guardio/issues)
