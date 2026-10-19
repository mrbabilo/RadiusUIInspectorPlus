# Tests en jeu

Mod de test d'Inspector Plus (`InspectorPlusTests/`, packageId `mrbabilo.RadiusUI.InspectorPlus.Tests`). Il n'est jamais publié : `deploy.sh` exclut `tests/`, et le zip de release est construit à partir du dossier publié.

Le mod ne fait rien sans l'un de ses deux arguments de lancement :
- `-ruitest=<fichier>` : contrôles automatiques. Le mod construit une scène sur la carte de test rapide et lance chaque contrôle une fois. Il écrit ensuite une ligne PASS ou FAIL par contrôle, puis quitte le jeu.
- `-ruiscene=<dossier>` : scènes de mesure pour Dubs Performance Analyzer, pilotées par fichier. Ce mode ne pose aucun patch Harmony.

Les scripts lancent une partie séparée, avec son propre dossier de sauvegarde (`-savedatafolder`), dans `$RUITEST_WORK` (par défaut `$TMPDIR/ruitest`). Ils touchent quand même aux préférences Unity du joueur (taille de fenêtre, plein écran). Pour cela :
- ils les sauvegardent une fois, dans le dossier de travail ;
- ils les remettent après chaque lancement.

Le mod de test n'est copié dans `Mods` que le temps du lancement.

## Contrôles

Publier d'abord Inspector Plus (`./deploy.sh`) : les contrôles portent sur le mod installé.

```sh
tests/run-checks.sh A
tests/run-checks.sh B "astryl.radiusui.everywhere astryl.radiusui.healthtab astryl.radiusui.needstab mrbabilo.radiusui.colonistbarplus"
tests/run-checks.sh C "ludeon.rimworld.royalty ludeon.rimworld.ideology ludeon.rimworld.biotech ludeon.rimworld.anomaly ludeon.rimworld.odyssey"
tests/run-checks.sh D "" -ruinoflatfix
```

Le journal de debug et la notice du framework Radius ne s'ouvrent pas pendant les tests et les scènes (`QuietStartup.cs`) ; les erreurs restent comptées par les contrôles.

Chaque script n'affiche que les lignes qui ne sont pas PASS, et rend le code 1 si un contrôle a échoué. Les résultats complets sont dans `$RUITEST_WORK/<nom>.txt`, et le journal du jeu dans `<nom>.log`.

Ce qui est vérifié :
- les patches ;
- les instantanés (pion sans Biotech, objet, sélection multiple) ;
- les filtres sauvegardés (remplacer, ajouter, retirer, filtre parent, def inconnu, sauvegarde puis chargement) ;
- le bouton Inverser, les matériaux et les filtres spéciaux repris des autres mods ;
- le mode du panneau selon la sélection, et l'onglet Santé affiché dans le panneau ;
- le panneau Stockage (stock, contenu, avertissement quand des objets stockés ne sont plus acceptés, Tout déplier) ;
- l'arbre Stockage : hauteur comparée à un parcours complet forcé (en haut, au milieu, en bas, après repli, pendant une recherche), et saut vers un objet depuis la liste du contenu ;
- le comportement des colons avec les filtres spéciaux (`HaulChecks.cs`). Deux zones de stockage aux filtres opposés, des objets posés au sol entre elles, puis le jeu avance tick par tick jusqu'à ce que les colons aient tout rangé. Chaque objet doit finir dans la zone attendue. Paires testées : objets inutiles et utiles (vêtements, armes), avec ou sans qualité, périssables ou non, inflammables ou non, empilables ou non, avec ou sans points de vie, et filtre de matériau acier. Plus : un tas déjà stocké puis refusé, signalé « sur le départ » dans le panneau puis déplacé par un colon ; un filtre de zone aux filtres spéciaux refusés, sauvegardé puis rechargé. Pendant ces scénarios, le reste de la carte est neutralisé (autres objets interdits, autres stockages fermés), puis remis en état ;
- le dialogue Matériaux ;
- l'absence d'erreur et de retour au panneau vanilla.

## Mesures DPA

DPA (Workshop 2038874626) doit être installé. Ses réglages pour le bac à sable viennent de `tests/dpa/`. Les réglages DPA du joueur ne sont pas modifiés.

```sh
tests/run-scene.sh            # bloque jusqu'à la fermeture du jeu ; lancer dans un autre terminal
tests/scene.sh select zone
tests/scene.sh tab storage
tests/scene.sh quit
```

Commandes de `scene.sh` :
- `select pawn|pawns|pawns3|bed|zone|shelf|none`
- `tab storage|health|gear|needs|none`
- `materials`, `closeall`, `speed <0-4>`, `shot <fichier.png>`, `quit`
- `fluency pawn|health|pawns|zone|shelf [images] [vanillafirst]` : sonde de fluidité, sans DPA (voir plus bas)

Le panneau DPA s'ouvre par la loupe de la barre du bas, catégorie Custom Update.

Deux pièges de mesure :
- **Méthode récursive** (`DrawNode`) : ne la mesurer que depuis le site d'appel de son appelant, jamais avec un second profileur DPA sur elle.
- **Repli automatique** : le panneau se replie quand la souris le quitte. Un clic sur le panneau replié tombe sur la carte.

## Fluidité : allocations, GC et saccades

La sonde `fluency` mesure ce que les moyennes de DPA ne montrent pas. Toutes les 600 images, elle passe d'un état à l'autre, sur la même carte et au même moment de la partie :
- `off` : rien de sélectionné ;
- `plus` : la scène, dessinée par Inspector Plus ;
- `vanilla` : la même scène avec Inspector Plus désactivé.

La part propre à Inspector Plus est « plus − vanilla ». Des parties séparées ne se comparent pas : chaque test rapide tire une carte et des colons au hasard.

```sh
tests/run-scene.sh &
tests/scene.sh speed 1
tests/scene.sh fluency zone 5400     # rend la main tout de suite ; 3 blocs par état
# attendre $RUITEST_WORK/fluency-zone.txt
```

Scènes : `pawn` (un colon), `health` (un colon, onglet Santé), `pawns` (20 colons), `zone` et `shelf` (onglet Stockage).

L'ordre par défaut est off, plus, vanilla. L'état plus exécute aussi du code vanilla (onglets hébergés, dessin de la sélection) : dans une partie neuve, il paie seul le premier passage de ce code. Pour mesurer la première ouverture du panneau, ajouter `vanillafirst` dans une partie neuve.

Pour chaque état, la sonde donne :
- ce qui était réellement à l'écran (mode du panneau, onglet ouvert) ;
- la mémoire allouée par image, en moyenne et par bloc, mesurée par la somme des hausses de `GC.GetTotalMemory` (le compteur par thread vaut toujours 0 sous le GC de Unity) ;
- les collectes de génération 0 ;
- les temps d'image p50, p95, p99, max, et le nombre d'images au-delà de 25 et 40 ms ;
- à part, les 30 premières images après chaque bascule : coût ponctuel de l'ouverture du panneau.

Pendant la mesure, le plafond de fréquence est levé : au plafond de 30 ips, toutes les images durent 33 ms. Le panneau est aussi épinglé, sinon il se replie au bout de quelques secondes. Les deux réglages sont remis en fin de mesure.

Le jeu doit rester au premier plan : une fois caché derrière une autre fenêtre, il se fige.

Rapports de mesure : `docs/perf/`.
