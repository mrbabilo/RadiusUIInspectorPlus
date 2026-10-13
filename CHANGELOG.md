# Journal des modifications

Le mod n'a pas de numéro de version (`About.xml` n'en déclare pas). Les entrées sont datées, les plus récentes en premier.

## 2026-10-14

### Ajouts

- **Repères d'objets dans l'arbre des filtres.** Chaque ligne d'objet porte des pastilles de couleur pour ses propriétés. Une infobulle détaille chacune :
  - type d'aliment : végétal, produit animal (lait, œufs), viande animale, viande d'insecte, viande humaine. Les repas et les cadavres n'en ont pas, car leur type dépend de chaque objet ;
  - toxique : drogue, drogue addictive, déchet toxique, gaz toxique (obus, grenades), aliment qui intoxique ;
  - périssable, avec le nombre de jours avant de se gâter ;
  - incinérable (détruit par une recette d'incinération) ;
  - recyclable (fondu à la fonderie). Les métaux bruts n'ont pas ce repère ;
  - vêtement réservé aux enfants, aux hommes ou aux femmes.

  Les pastilles occupent une colonne fixe, juste avant la quantité stockée : elles restent alignées d'une ligne à l'autre, avec ou sans stock. Quand la propriété dépend du matériau, la pastille est atténuée. Sur une ligne refusée, les pastilles pâlissent comme le nom.
- **Filtre par propriété.** Sous la recherche, une puce par propriété présente dans l'arbre. Un clic n'affiche que les objets qui l'ont ; plusieurs puces choisies doivent toutes s'appliquer. Le filtre se combine avec la recherche, et se remet à zéro au changement de stockage.
- **Bouton « Tout déplier ».** « Tout replier » devient une bascule : il replie l'arbre si une catégorie est ouverte, et déplie tous les niveaux sinon. Le libellé dépend de l'arbre affiché, pas des autres stockages. Le bouton est désactivé pendant une recherche ou un filtre par propriété, qui déplient déjà tous les résultats.

### Modifications

- **Champ de recherche mis en évidence** : loupe, contour coloré (plein quand le champ est actif ou contient une recherche), fond qui s'éclaircit au survol. Cliquer sur la loupe place le curseur dans le champ.
- **Panneau Stockage plus léger** :
  - la visibilité de chaque objet est mémorisée, au lieu de refaire à chaque image le test des filtres spéciaux du filtre parent ;
  - l'icône et la couleur de chaque objet sont calculées une seule fois ;
  - les infobulles ne sont construites que pour la ligne survolée.

## 2026-10-13

Première version : fork de Radius UI - Inspector (astryl), avec RimKeeper Storage and Filters Helper (Keepercraft) fusionné dans le panneau Stockage.

### Ajouts

- Filtres enregistrés, partagés entre les parties, applicables en mode remplacer, ajouter ou retirer. On peut les renommer ou les écraser avec le filtre actuel.
- Cochage par glissement dans l'arbre des filtres.
- Liste du contenu du stockage. Un clic sur une ligne ouvre l'objet dans l'arbre.
- Quantité stockée sur chaque ligne d'objet, en orange quand les réglages refusent l'objet. Un message prévient quand un changement fait sortir des objets déjà stockés.
- Filtres spéciaux reliés à l'arbre. Une pastille marque les lignes concernées par un filtre qui dépend de l'état de chaque objet.
- Une couleur par grande catégorie. Les filtres spéciaux sont verts ou rouges selon ce qu'ils laissent entrer.
- Icônes d'objets agrandies et recadrées.
- Réglage de la hauteur du panneau Stockage.
