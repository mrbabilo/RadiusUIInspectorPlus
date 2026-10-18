# Journal des modifications

Les versions suivent `<modVersion>` dans `About.xml` et les releases GitHub. Les plus récentes en premier.

## 1.2.3 — 2026-10-18

### Corrections

- **Sans Biotech** : le panneau d'un pion ne plante plus. Le xénotype n'est lu que si Biotech est actif ; sinon la ligne affiche la race.
- **Performance des zones de défilement** : le défilement du framework Radius reconstruisait 16 fois le cache des styles de l'interface à chaque affichage d'une zone de défilement (environ 0,5 ms par image, saccades jusqu'à 10 ms). Un correctif, actif seulement avec la version 32 du framework, pose les styles une seule fois ; l'option de lancement `-ruinoflatfix` le désactive. Le même correctif est livré avec Colonist Bar Plus : le premier des deux chargé l'applique.

## 1.2.2 — 2026-10-15

### Corrections

- **Onglets du jeu dans l'Inspector** : un onglet plus étroit que le panneau (équipement, dressage, visiteur…) prend maintenant toute la place au lieu de laisser une bande vide à droite. L'onglet retrouve sa propre taille dès qu'il n'est plus affiché dans l'Inspector.
- **Radius UI - Health Tab et Nice Health Tab** : après être passé de l'onglet santé Radius à l'onglet du jeu (ou de Nice Health Tab), la carte de santé n'est plus déformée. Radius laissait sa hauteur en place ; la taille d'origine de l'onglet est maintenant rétablie au changement.
- **Bio Tab+** : sa fiche remplit tout le panneau de l'Inspector au lieu de laisser une bande vide à droite.
- **Modern Bio Tab** : dans l'Inspector, le bouton « Modern Bio » (affiché quand l'onglet Modern Bio est désactivé) ne touche plus les boutons de la fiche du jeu. Il devient une icône (celle de la disposition en cartes de Modern Bio, infobulle « Modern Bio ») : à droite de la fiche du jeu quand le panneau laisse de la place, sinon au-dessus de la fiche, à gauche de la croix de fermeture ; sur la fiche Bio Tab+, dans l'en-tête, à gauche de la roue dentée, au lieu de recouvrir ses icônes ; un clic passe à Modern Bio et désactive Bio Tab+, qui se réactive depuis la fiche du jeu.
- **Titres de section traduits** : les titres des mises en page fournies (Besoins, Santé, Compétences, Équipement…) s'affichaient en anglais, y compris dans les mises en page déjà enregistrées. Un titre tapé par le joueur reste tel quel.
- **Filtres spéciaux** : sur une ligne à un seul choix (« Qualité », armes ou vêtements sans qualité), la catégorie s'aligne avec celles des autres lignes au lieu de coller au bord droit.

## 1.2.1 — 2026-10-14

### Corrections

- **Barre de l'éditeur de mise en page** : les boutons prennent la largeur de leur libellé et passent à la ligne quand la place manque. « Réinitialiser la mise en page », « Copier depuis… », « Préréglages… » et « Panneau de gauche » ne sont plus coupés.
- **Filtres de No Quality Filter** : ils se rangent dans une ligne « Qualité », avec Armes ou Vêtements en bout de ligne, au lieu d'une ligne sans titre. Libellés français : « Les armes sans qualité », « Les vêtements sans qualité ».
- **Better Filter** : sa ligne « périssable » s'intitule « Périssable » et non plus « Pourriture ». Elle compte aussi les objets qui se dissolvent, donc elle ne fait pas double emploi avec la ligne « Pourriture ».
- **Better Filter en français** : ses dix filtres (périssable, détérioration, qualité, points de vie, empilable) ont un libellé et une description en français, chargés seulement quand Better Filter est actif.

## 1.2.0 — 2026-10-14

Trois mods de filtres sont intégrés au panneau Stockage : Useful Filters (kentotsu), Useless Filter (Mlie) et Material Filter (CheaterEater). Inspector Plus les déclare incompatibles : désactivez-les. Tant que l'un d'eux reste actif, Inspector Plus ne charge pas sa propre version de ces filtres et affiche ceux du mod.

No Quality Filter (rabiosus) est aussi repris et déclaré incompatible. Ses deux filtres « sans qualité » (armes, vêtements) gardent leurs noms de sauvegarde : un stockage qui les refusait continue de les refuser.

### Ajouts

- **Infobulles détaillées dans le panneau des pions** (contenu repris de RimHUD, couleurs Radius). Santé : chaque problème avec sa partie du corps, coloré selon sa gravité, précédé du saignement. Humeur et état mental : chaque pensée avec son effet sur l'humeur, et l'inspiration. Sexe et âge : titre, faction, idéologie, xénotype, enfance, âge adulte, travaux impossibles, sensibilité psychique, méditation (pour un animal : dressabilité, décroissance du dressage, régime, maître). Traits : la description de chaque trait. Besoins : le texte du jeu, plus le régime et la vitesse de repas pour la nourriture, le taux de repos pour le sommeil, beauté, confort et plein air pour le loisir. Compétences : la description du jeu, la passion et les statistiques liées à chaque compétence. Activité : le type de travail et ses compétences. Capacités : le détail du jeu. Le texte n'est calculé qu'au survol.
- **Préréglages de mise en page du joueur.** Le bouton « Préréglages… » de l'éditeur de mise en page enregistre le corps et la colonne des quatre profils (colon, animal, mécanoïde, prisonnier) sous un nom, partagé entre les parties. Chaque préréglage s'applique, s'écrase avec la mise en page actuelle, se renomme ou se supprime (deux clics).
- **Import des mises en page RimHUD**, sans que RimHUD soit chargé : la mise en page RimHUD actuelle et les préréglages du joueur (`Config/RimHUD`), ceux livrés avec RimHUD et ceux des dossiers `RimHUD/Presets` des autres mods. L'import ajoute un préréglage sans l'appliquer. Chaque widget RimHUD devient la case Inspector équivalente, selon les cibles de RimHUD (colons, autres humains, créatures). Le préréglage garde la colonne actuelle de chaque profil, et les cases déjà dans cette colonne ne sont pas répétées dans le corps. Les widgets sans équivalent (widgets d'autres mods, séparateurs) sont laissés de côté et listés.
- **Matériaux.** Un filtre par matériau (acier, bois, chaque cuir, chaque tissu…) : le refuser refuse tout ce qui en est fait. Le panneau les range dans une section « Matériaux » repliable, avec l'icône de chaque matériau, en ne gardant que les matériaux dont le stockage peut prendre un objet. La case de l'en-tête autorise ou refuse tous les matériaux d'un coup. Les factures, les tenues et l'onglet Stockage vanilla ne les mêlent pas à leur arbre : un bouton « Matériaux » au-dessus de leur filtre ouvre une fenêtre avec les matériaux qui les concernent (recherche, icônes, bascule tout autoriser / tout refuser, cochage par glissement).
- **Nouveaux filtres spéciaux**, par paires : avec ou sans qualité, se détériore ou non, pourrit ou non, inflammable ou non (repris de Useful Filters) ; avec ou sans points de vie, empilable ou non (repris de [UR] Better Filter, chargés seulement sans lui) ; cadavres d'animaux de la colonie ou des autres.
- **Vêtements et armes inutiles ou utiles** (repris de Useless Filter). Les conditions se règlent dans les réglages du mod : plage de points de vie, plage de qualité, contaminé, propre, biocodé, non biocodé. Une seule condition remplie suffit.
- **Bouton « Inverser »** : autorise chaque objet refusé et refuse chaque objet autorisé. Les filtres spéciaux gardent leur état.
- **Compatibilité [UR] Better Filter** : un bouton « Avancé » ouvre son filtre avancé. Ses deux objets factices (« advanced filter », « apply filter ») ne s'affichent plus comme des objets cochables.
- **Compatibilité Storage Sorting** : une roue dentée sur chaque ligne ouvre son réglage fin. Elle reste allumée sur les lignes réglées.
- **Repère « produits frais »** : l'ancien repère « périssable » prend ce nom. Il marque ce qui doit être réfrigéré, avec le nombre de jours avant de se gâter.
- **Lignes exclusives reliées à l'arbre** : quand tous les choix d'une ligne qui se partage tous les objets qu'elle concerne sont refusés (frais et pourri, contaminé et propre, biocodé et non biocodé, recyclable et non recyclable, incinérable et non, corps de colons, d'esclaves, d'étrangers et anormaux, nos animaux et les autres, les paires de propriétés…), ces objets sont décochés dans l'arbre, puisque le stockage ne peut plus en prendre aucun. Autoriser de nouveau un choix les recoche, et seulement ceux-là. Les lignes qui ne se partagent pas tout (sexe, régime, livres, cadavres de mechs) ne touchent pas l'arbre.

### Modifications

- **Filtres spéciaux redondants masqués.** Deux filtres qui ont exactement le même effet sur ce stockage (ils refusent les mêmes objets) n'en font plus qu'un : le filtre vanilla reste en priorité, puis celui d'Inspector Plus. Un filtre qui refuserait tout ce que le stockage peut prendre est masqué aussi, car il fait la même chose que « Tout refuser ». Les paires de [UR] Better Filter (qualité, périssable, détérioration, points de vie, empilable) entrent dans la comparaison : elles disparaissent quand celles d'Inspector Plus font la même chose. La comparaison ne porte que sur les filtres décidés par le type d'objet seul ; ceux qui dépendent de chaque objet (pourri, contaminé) restent tous. Un filtre déjà refusé reste affiché.
- **Filtres spéciaux regroupés par ligne.** Chaque ensemble de choix tient sur une ligne titrée : État (frais, pourri), Contamination, Recyclage, Incinération, Biocodage, Corps humains, Compétences des livres, Qualité, Pourriture… Chaque choix est une puce reprenant le libellé du jeu sans son verbe, verte ou rouge selon ce qu'elle laisse entrer ; une puce refusée est pâle et barrée. Les puces passent à la ligne si besoin. Une ligne de plusieurs choix finit par une case « Tout », allumée quand tous ses choix sont autorisés : un clic les autorise ou les refuse tous, et on peut glisser d'une case « Tout » à l'autre. La catégorie concernée (Vêtements, Armes) s'affiche en bout de ligne quand deux lignes portent le même titre. Les filtres d'autres mods se rangent par paire « X / Non X », ou seuls. Le cochage par glissement marche d'une puce à l'autre.
- **« Tout autoriser » et « Tout refuser » deviennent un seul bouton à bascule**, dans le panneau comme dans la fenêtre Matériaux. Il autorise tout tant que tout n'est pas autorisé, puis refuse tout.
- **Couleurs des nouveaux filtres** : ce qui peut pourrir, s'user dehors, prendre feu ou être inutile est en rouge ; l'inverse en vert.
- Un filtre spécial ne s'affiche plus que si sa propre catégorie contient un objet du stockage (« sans qualité (armes) » n'apparaît plus sur une étagère à vêtements).
- La plage de qualité s'affiche sans Royalty, comme en vanilla.

### Écarts avec les mods repris

- Les sauvegardes gardent leurs réglages : les filtres reprennent les defName d'origine, et les matériaux refusés enregistrés par Material Filter sont relus au chargement.
- Material Filter refusait une armure ou une arme selon les matériaux de sa recette, et la gardait tant qu'un d'eux restait autorisé. Ici, un filtre de matériau s'applique au matériau dont l'objet est fait, comme tout filtre spécial vanilla. Le bouton de Material Filter dans les factures et les tenues est remplacé par la barre « Matériaux ».
- Useful Filters disait « tous les cadavres » pour ses deux filtres de cadavres d'animaux : en refuser un refusait tous les cadavres d'animaux. Chaque filtre ne juge plus que l'animal de chaque cadavre.
- Inflammable : Useful Filters jugeait sur le def seul, donc un vêtement en tissu comptait comme ininflammable. Le matériau de chaque objet décide désormais.
- Useless Filter ne jugeait une arme biocodée inutile que si elle était codée pour un de vos colons. Une arme ou un vêtement biocodé est désormais inutile s'il est codé pour quelqu'un d'autre qu'un de vos colons. Les armes à persona ne comptent pas.
- Les réglages par filtre de Useful Filters (afficher ou masquer chacun) ne sont pas repris.
- Les conditions « inutile » de Useless Filter sont des réglages du mod, pas des données de partie. Au premier lancement qui trouve son fichier de réglages (`Config/Mod_<dossier>_UselessFilterMod.xml`), Inspector Plus les recopie dans ses propres réglages. Tant que ce fichier existe, un bouton des réglages permet de les reprendre à nouveau. Sans ce fichier, les conditions partent des valeurs par défaut de Useless Filter (points de vie 0–50 %, qualité affreux à médiocre, contaminé, biocodé).

### Sauvegardes

Le mod n'est plus purement visuel : les filtres ajoutés sont écrits dans les sauvegardes. Le retirer en cours de partie fait apparaître des avertissements au chargement et oublie ces réglages.

## 1.1.0 — 2026-10-14

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

## 1.0.0 — 2026-10-13

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
