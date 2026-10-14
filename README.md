# Radius UI - Inspector Plus

Mod RimWorld 1.6 qui remplace le panneau d'inspection en bas à gauche, avec un panneau Stockage enrichi.

C'est un fork de **Radius UI - Inspector** (astryl), dans lequel **RimKeeper Storage and Filters Helper** (Keepercraft) est fusionné. Il remplace ces deux mods : désactivez-les.

- packageId : `mrbabilo.RadiusUI.InspectorPlus`
- Auteur : mrbabilo
- Dépendances : [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) et [Radius UI Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3786107692)

## Ce que Plus ajoute au panneau Stockage

- **Filtres enregistrés**, partagés entre les parties. On peut les appliquer en mode remplacer, ajouter ou retirer. On peut aussi les renommer, ou les écraser avec le filtre actuel.
- **Cochage par glissement** : glisser sur les cases coche ou décoche plusieurs lignes d'un coup.
- **Liste du contenu** : ce que contient le stockage, une ligne par objet avec sa quantité. Un clic sur une ligne ouvre l'objet dans l'arbre des filtres.
- **Stock dans l'arbre** : chaque ligne d'objet affiche la quantité stockée ici. La quantité passe en orange quand les réglages refusent ces objets. Un message prévient quand un changement fait sortir des objets déjà stockés.
- **Filtres spéciaux reliés à l'arbre** : refuser « frais », « fondable »… décoche les objets que ce filtre refuse toujours. Les filtres qui dépendent de l'état de chaque objet (pourri, contaminé) marquent d'une pastille les lignes concernées.
- **Repères d'objets** : chaque ligne d'objet porte de petites icônes pour ses propriétés. Une infobulle donne le détail de chacune :
  - type d'aliment : végétal, produit animal, viande, viande d'insecte, viande humaine ;
  - toxique : drogue (addictive ou non), déchet toxique, gaz toxique, aliment qui intoxique ;
  - périssable, avec le nombre de jours avant de se gâter ;
  - incinérable ;
  - recyclable ;
  - vêtement réservé aux enfants, aux hommes ou aux femmes.

  Quand la propriété dépend du matériau, l'icône est atténuée.
- **Recherche mise en évidence** : le champ de recherche de l'arbre a une loupe et un contour coloré, plein quand le champ est actif.
- **Tout replier / Tout déplier** : un seul bouton fait les deux, selon l'état de l'arbre affiché. Il est désactivé pendant une recherche, qui déplie déjà tous les résultats.
- **Couleurs** : une couleur par grande catégorie. Les filtres spéciaux sont verts ou rouges selon ce qu'ils laissent entrer.
- **Icônes d'objets agrandies**, recadrées sur le dessin visible.
- **Réglage de la hauteur** du panneau Stockage.
- **Inverser** : autorise chaque objet refusé et refuse chaque objet autorisé.
- **Matériaux** : un filtre par matériau, rangé dans une section à part avec l'icône du matériau. Refuser un matériau refuse tout ce qui en est fait. Les factures, les tenues et l'onglet Stockage vanilla ont un bouton « Matériaux » au-dessus de leur filtre.
- **Filtres spéciaux supplémentaires** : avec ou sans qualité, se détériore ou non, pourrit ou non, inflammable ou non, avec ou sans points de vie, empilable ou non, cadavres d'animaux de la colonie ou des autres, vêtements et armes inutiles ou utiles (conditions dans les réglages du mod).

## Mods remplacés ou pris en charge

Inspector Plus reprend **Useful Filters**, **Useless Filter** et **Material Filter**, et les déclare incompatibles. Une partie qui les utilisait garde ses réglages. Tant que l'un d'eux reste actif, Inspector Plus lui laisse ses filtres (`LoadFolders.xml`).

Avec **[UR] Better Filter**, un bouton « Avancé » ouvre son filtre avancé, et ses objets factices restent hors de l'arbre. Avec **Storage Sorting**, une roue dentée sur chaque ligne ouvre son réglage fin.

Le mod écrit ces filtres dans les sauvegardes. Le retirer en cours de partie fait apparaître des avertissements au chargement, et ces réglages sont perdus.

## Compiler

Le dépôt ne contient pas la DLL. Le projet `Source/RadiusUIInspector.csproj` (`net472`) se compile contre les DLL du jeu (RimWorld 1.6), contre `0Harmony.dll` et contre `RadiusUI.Framework.dll` installé. Adaptez les `HintPath` du `.csproj` à votre installation.

`deploy.sh` compile hors de l'arbre, copie la DLL dans `Assemblies/` et publie le mod dans le dossier `Mods` du jeu (chemins à adapter en tête du script).

Pour rester compatible avec les autres mods Radius UI, gardez le namespace `RadiusUIInspector`, le nom de DLL `RadiusUIInspector.dll` et l'identifiant Harmony `astryl.radiusui.inspector`.

## Sources et crédits

| Composant | Auteur | Source | Licence |
|---|---|---|---|
| Radius UI - Inspector (base du mod) | astryl | [Steam Workshop 3786131805](https://steamcommunity.com/sharedfiles/filedetails/?id=3786131805) | MIT |
| RimKeeper Storage and Filters Helper (filtres enregistrés, cochage par glissement, hauteur du panneau, liste du contenu) | Keepercraft | [github.com/keepercraft/RimKeeperFilterHelper](https://github.com/keepercraft/RimKeeperFilterHelper) | MIT |
| Useful Filters (idée et defName des filtres de propriétés, réécrits) | kentotsu | [Steam Workshop 3766551975](https://steamcommunity.com/sharedfiles/filedetails/?id=3766551975) | non indiquée |
| Useless Filter (idée et defName des filtres « inutile », réécrits) | Mlie | [github.com/emipa606/UselessFilter](https://github.com/emipa606/UselessFilter) | MIT |
| Material Filter (idée et defName des filtres de matériau, réécrits) | CheaterEater, d'après Kami Katze | [Steam Workshop 3698331639](https://steamcommunity.com/sharedfiles/filedetails/?id=3698331639) | GPL-3.0 |
| Radius UI Framework (dépendance, non incluse) | astryl | [Steam Workshop 3786107692](https://steamcommunity.com/sharedfiles/filedetails/?id=3786107692) | — |
| Harmony (dépendance, non incluse) | Andreas Pardeike | [github.com/pardeike/HarmonyRimWorld](https://github.com/pardeike/HarmonyRimWorld) | MIT |

Les sources C# d'Inspector viennent d'une décompilation (ILSpy) de la version du Workshop. Elles ont ensuite été modifiées. Le code de RimKeeper a été réécrit pour l'interface Radius UI. Il ne reste pas de copie de son code d'origine.

L'icône et l'image d'aperçu (`About/ModIcon.png`, `About/Preview.png`) sont celles du mod d'origine d'astryl.

Merci à astryl pour la suite Radius UI, et à Keepercraft pour RimKeeper.

## Licence

MIT, voir `LICENSE`. La notice d'astryl est conservée, et les modifications de mrbabilo y sont ajoutées.
