# Feuille de route – RPH Revamped Project Hospital

État au 10 octobre 2026. Les faits techniques viennent de la décompilation du jeu installé
(1.2.x, 4 DLC). Les problèmes de joueurs viennent des discussions Steam et d'agrégats d'avis
(voir « Sources » en bas). Beaucoup de fils datent de 2018–2021 : certains bugs ont pu être corrigés.

## 1. Ce que la décompilation change au plan

1. **Le jeu a son propre chargeur de mods de code.** Il charge les DLL d'un dossier
   `StreamingAssets/Addons/Mod…/` si ce dossier contient `0Harmony.dll` et si le joueur a activé
   « Enable loading of code mods » dans les options. Le point d'entrée est une classe qui hérite de
   `HospitalMod`. **BepInEx n'est donc pas nécessaire**, et c'est tant mieux : il est pénible à
   installer, surtout sur macOS.
2. **Harmony est obligatoire** : le chargeur exige `0Harmony.dll` dans le dossier. On s'en sert pour
   tous les patchs.
3. **Le Workshop ne livre pas les DLL.** Les abonnés reçoivent seulement le XML. Il faudra distribuer
   un zip (GitHub Releases) avec une procédure de copie, comme le fait PooK.
4. **Tout l'argent passe par deux méthodes** : `Hospital.Pay` (toutes les recettes, y compris celles
   des départements via `Department.Pay`) et `Hospital.Spend` (toutes les dépenses). Un grand livre de
   comptes complet est faisable avec quelques patchs, sans toucher au reste.
5. **Le jeu ne garde que des totaux par jour** (`DailyStatistics`, par hôpital et par département).
   Pas de détail par équipement, par équipe ou par patient. C'est la raison pour laquelle la fenêtre
   de stats doit commencer par un grand livre à nous.
6. **L'interface du jeu est en uGUI classique**, avec des prefabs réutilisables
   (`Resources.Load("Prefabs/UI/…")` : lignes de tableau, segments, icônes) et un atlas d'icônes
   (`Textures/ui/icons.png`). On peut construire nos fenêtres avec les briques du jeu, chargées depuis
   l'installation du joueur, sans copier ses fichiers dans le dépôt.
7. **La capture d'écran de référence vient du mod « PooK Hospital Economy »** (Workshop 3815904306).
   Il n'a pas de licence : on s'inspire de l'idée et de la mise en page, mais on réécrit tout. Il
   patche les mêmes méthodes et ajoute son propre barème : nos modules économie devront le détecter
   et prévenir le joueur pour éviter une double facturation.

## 2. Problèmes récurrents

| Domaine | Problème | Origine | Réponse du mod | Phase |
|---|---|---|---|---|
| Interface | UI jugée « écrasante et peu intuitive », dure à gérer pour un grand hôpital, illisible sur Steam Deck | Avis Steam | Fenêtres de stats détaillées, lisibles, avec explications au survol | 2 |
| Interface | Impossible de savoir pourquoi on perd de l'argent : la radiologie et le labo paraissent toujours déficitaires | Avis, code | Grand livre par département, équipement et équipe | 1–2 |
| Économie | Début de partie très serré, afflux de patients aléatoire d'un jour à l'autre | Steam | Lisser l'afflux, paiement journalier de l'hospitalisation | 3 |
| Économie | Une fois lancé, le jeu « tourne tout seul » | Steam (2024) | Coûts fixes qui croissent avec la taille, entretien, contrats | 3, 5 |
| Économie | Les examens et traitements ne coûtent rien à l'hôpital, l'équipement n'a aucun coût d'entretien | Code | Barème de coûts variables, entretien et amortissement | 3, 5 |
| Économie | L'hospitalisation n'est payée qu'à la sortie : patients bloqués = aucune recette | Steam, code | Paiement journalier | 3 |
| Économie | Prêt rudimentaire : paliers fixes de 20 000, plafond 500 000 | Code | Taux selon le prestige, échéancier | 3 |
| Économie | Les frais d'agence de recrutement sont comptés comme des « pénalités » | Code | Catégorie propre dans nos stats | 1 |
| RH | Le moral n'a aucune conséquence : le personnel ne démissionne jamais | Steam | Moral avec effets : fatigue, absences, démissions | 4 |
| RH | Niveau, compétences et salaire mal reliés, salaire figé à l'embauche | Steam, code | Négociation, augmentations, primes | 4 |
| RH | Relève des équipes à 7 h et 20 h : l'hôpital se bloque une à deux heures | Steam | Chevauchement des équipes | 4 |
| RH | Agents d'entretien : un par département et un local chacun, pénible | Steam | Service de nettoyage par zones | 4 |
| RH | Alertes « personnel manquant » erronées, plafond de 200 employés | Steam (2018) | Le plafond est maintenant une option du jeu ; diagnostic dans nos stats | 4 |
| Matériel | Pas d'usure, de panne ni de gamme : l'achat est un choix unique | Code, Steam | Usure, pannes, maintenance, gammes | 5 |
| Matériel | Labos et radiologie saturés, patients en attente d'examens | Steam | Indicateurs de file d'attente et taux d'utilisation | 2, 5 |
| Recherche | Aucun système de recherche ; demande d'un hôpital universitaire | Steam | Arbre de recherche, internes et étudiants | 6 |
| Domaines | Les mods de départements dupliquent symptômes et examens ; le CRP qui les fédère n'est plus maintenu | Workshop | Contenu propre préfixé `RPH_`, CRP optionnel | 7 |
| Flux patients | Patients bloqués (observation, hospitalisation), départs après un délai | Steam | Onglet « patients bloqués » avec la cause | 2, 7 |
| Performance | Ralentissements sur les grands hôpitaux, plantages | Avis Steam | Règle : aucun calcul par image, agrégats par heure ou par jour | Toutes |

## 3. Dépendances

| Dépendance | Décision | Raison |
|---|---|---|
| Harmony (MIT) | **Oui**, livré en `0Harmony.dll` (build net35) | Exigé par le chargeur du jeu. |
| BepInEx | **Non** | Inutile avec le chargeur natif, difficile sur macOS. On reste compatible avec les mods qui l'utilisent (Hospital EMS). |
| Community Resource Pack | **Plus tard, optionnel** (phase 7) | Utile seulement pour du contenu médical partagé. Plus maintenu depuis environ 2022 : risque. |
| PooK Hospital Economy | **Incompatible** avec nos modules économie | Mêmes patchs, double facturation. Détection et avertissement. |

## 4. Plan étape par étape

Chaque phase se termine par une version jouable. Les phases 1 et 2 ne changent pas le gameplay : elles
servent à mesurer, pour équilibrer tout le reste avec des chiffres.

**État au 10 octobre 2026** : phases 0, 1 et 2 codées, compilées et déployées sur macOS ; 45 tests
unitaires du cœur passent. **Reste à vérifier en jeu** (critères « Terminé quand » ci-dessous), puis
sous Windows. Écarts par rapport au plan initial, pour rester simple :
- le registre stocke des totaux par jour (catégorie × département × cause × équipe), pas chaque
  mouvement : c'est assez pour toutes les vues prévues et le fichier reste petit ;
- fichier annexe `<sauvegarde>.rph.txt` (texte tabulé, sans dépendance JSON en .NET 3.5) ;
- la fenêtre est construite en code avec la police, les icônes de départements, les couleurs du
  Budget et un bouton d'onglet cloné du jeu ; les prefabs `Prefabs/UI/*` ne sont pas utilisés ;
- pas d'onglet « Patients » ni d'infobulles : chaque ligne affiche directement son nombre (« ×87 ») ;
- pas de registre d'onglets générique : chaque phase ajoutera son onglet directement.

### Phase 0 – Fondations

1. Créer la solution .NET : `RPH.Core` (C# pur, `net35` + `net8.0`), `RPH.Mod` (`net35`, références
   aux DLL du jeu avec `Private=false`), `RPH.Core.Tests` (xUnit). Chemin du jeu dans `local.props`,
   ignoré par git.
2. Ajouter Harmony (NuGet `Lib.Harmony`, build net35), copié en `0Harmony.dll` avec sa licence.
3. Cible MSBuild `Deploy` : copie la DLL, Harmony et le dossier `mod/` dans
   `Addons/ModRPHRevamped/`.
4. Point d'entrée `RPHModEntry : HospitalMod` : logger `[RPH]`, `PatchAll`, lecture de la
   configuration, détection des autres mods (PooK, Hospital EMS).
5. **Test de faisabilité sur macOS** : un Postfix sur `Hospital.Pay` qui écrit dans `Player.log`.
   Puis le même test sous Windows, où joue la majorité des joueurs.
6. Trouver les points d'accroche de sauvegarde et de chargement. Écrire et relire un fichier annexe
   `<sauvegarde>.rph.txt`.
7. CI GitHub Actions : compiler et tester `RPH.Core` (les DLL du jeu ne peuvent pas être sur la CI).

**Terminé quand** le mod se charge sur macOS et Windows, et qu'une donnée survit à
sauvegarde → quitter → recharger.

### Phase 1 – Grand livre des comptes (aucun changement de gameplay)

1. Modèle `LedgerEntry` dans `RPH.Core` : jour, heure, montant, sens, catégorie, département,
   source (équipement, procédure, rôle), équipe de jour (08–20 h) ou de nuit (20–08 h).
2. Patchs : Prefix sur `Department.Pay` (département + patient), Postfix sur `Hospital.Pay` et
   `Hospital.Spend`, Prefix/Postfix sur `Department.PayWages` (département + employé), compteurs sur
   les procédures (utilisation par équipement, même quand elles ne coûtent rien).
3. Agrégats par jour, département, catégorie, équipe et équipement. Détail conservé 30 jours,
   totaux journaliers conservés pour toujours.
4. Persistance dans le fichier annexe. Les jours antérieurs à l'installation sont repris des
   `DailyStatistics` du jeu, marqués « non détaillés ».
5. Contrôle automatique chaque soir : somme du grand livre = variation du solde. Tout écart est
   journalisé.

**Terminé quand** 10 jours de jeu passent sans un dollar d'écart.

### Phase 2 – Fenêtre « Statistiques avancées »

1. Kit d'interface réutilisable : fenêtre déplaçable et fermable, onglets, tableaux, barres de
   pourcentage, graphique en barres, infobulles. Construit avec les prefabs `Prefabs/UI/*`, l'atlas
   `icons.png` et les couleurs de l'onglet Budget du jeu.
2. Accès par un nouvel onglet du panneau de gestion de l'hôpital (Postfix sur
   `HospitalManagementPanelController`) et par un raccourci clavier.
3. Onglets : Résumé, Dépenses, Recettes, Équipes, Départements, Équipements, Patients.
   Périodes : aujourd'hui, hier, 7 jours, 30 jours, tout, ou un jour précis.
4. Explication au survol de chaque ligne (exemple : « Scanner : 87 fois × 5 250 $ »).
5. Textes en anglais et en français via les tables de chaînes du jeu.
6. Performance : la fenêtre se recalcule seulement quand elle est ouverte, au plus une fois par heure
   de jeu. Lisibilité vérifiée en 1280×720 et 1280×800 (Steam Deck).

**Terminé quand** les chiffres correspondent à l'onglet Budget du jeu, et que la fenêtre ouverte ne fait
pas chuter les images par seconde. **Première version publique (alpha)** : utile seule, elle permet de
recruter des testeurs avant de toucher au gameplay.

### Phase 3 – Économie

1. Barème de coûts des examens et traitements, et remboursement par l'assurance selon le niveau de
   soin (consultation, observation, service, soins intensifs). Tous les montants dans des fichiers
   de données.
2. Paiement journalier de l'hospitalisation, au lieu d'un paiement unique à la sortie.
3. Prêts : taux selon le prestige, échéancier, découvert pénalisé.
4. Contrats d'assurance : tarifs négociés, plafonds de volume.
5. Courbe de difficulté : lisser le début de partie, ajouter des coûts fixes qui croissent avec la
   taille de l'hôpital pour éviter le « jeu qui tourne tout seul ».
6. Équilibrage avec la fenêtre de stats : tableaux de marges cibles par taille d'hôpital, préréglages
   Facile, Normal, Difficile.

### Phase 4 – Recrutement et personnel

1. Marché de l'emploi : vivier de candidats selon le prestige et le salaire proposé, délai de
   recrutement, postes ouverts.
2. Salaires : négociation à l'embauche, augmentations, primes.
3. Moral avec conséquences visibles : fatigue, absences, démissions.
4. Chevauchement des équipes pour supprimer le blocage de 7 h et 20 h.
5. Parcours de formation (en lien avec la formation du DLC Hospital Services).
6. Service de nettoyage par zones.
7. Onglet stats Personnel : coût et productivité par employé, départs, heures.

### Phase 5 – Matériel

1. Usure, pannes (événements), maintenance par des techniciens ou un contrat.
2. Gammes d'équipement (basique, standard, haut de gamme) : vitesse, précision du diagnostic, confort.
3. Indicateurs de saturation des labos et de la radiologie.
4. Nouveaux objets avec nos propres graphismes (atlas isométriques 4 directions, au format des
   tutoriels officiels), listés dans `CREDITS.md`.
5. Onglet stats Matériel : taux d'utilisation, rentabilité par machine, pannes.

### Phase 6 – Recherche

1. Points de recherche gagnés sur les cas complexes, les autopsies (DLC) et l'enseignement.
2. Arbre de recherche qui débloque des gammes d'équipement, des procédures et des gains d'efficacité.
   Vérifier si le panneau d'arbre de dépendances du jeu (`DependencyTreePanelController`) peut servir
   de base.
3. Hôpital universitaire : internes et étudiants.
4. Onglet stats Recherche.

### Phase 7 – Domaines (départements et contenu médical)

1. Décider entre dépendre du CRP ou créer notre propre contenu avec des identifiants `RPH_`.
2. Nouveaux départements et diagnostics en XML, avec des `ProcedureScript` en C# si besoin
   (le jeu les retrouve dans les types du mod).
3. Flux de patients : orientations entre départements, patients bloqués.

### Phase 8 – Publication et maintenance (en continu dès la phase 2)

1. Distribution : zip sur GitHub Releases, page Workshop avec la procédure de copie.
2. Matrice de compatibilité : avec et sans les 4 DLC, PooK, Hospital EMS, HZDev's QoL Mod.
3. Numéros de version, journal des modifications, migration du fichier annexe entre versions.
4. Test sur macOS et Windows à chaque version.

## 5. Risques et questions ouvertes

- **Harmony sur macOS** (Mono 2018, x86_64 sous Rosetta) : non vérifié. C'est le premier test de la
  phase 0.
- **Distribution manuelle** : chaque joueur doit copier le dossier et activer l'option. À rendre très
  simple (procédure illustrée, vérification au démarrage avec un message clair).
- **Taille fixe de l'interface du jeu** : nos fenêtres doivent gérer elles-mêmes leur mise à l'échelle.
- **Droits sur les assets du jeu** : les charger au lancement depuis l'installation du joueur ne pose
  pas de question de redistribution ; les copier dans le dépôt, si. Aucune politique écrite
  d'Oxymoron trouvée : leur demander par écrit avant de publier des dérivés de leurs graphismes.
- **Mises à jour du jeu** : les numéros de ligne changent ; refaire la décompilation et revérifier les
  points d'accroche après chaque mise à jour.

## Sources

Fils Steam remontés par les recherches, regroupés par thème (le rattachement précis de chaque
affirmation à un fil n'a pas été vérifié un par un) :

- Économie : [1](https://steamcommunity.com/app/868360/discussions/0/1752358461535394791),
  [2](https://steamcommunity.com/app/868360/discussions/0/1752358461531454087),
  [3](https://steamcommunity.com/app/868360/discussions/0/1752358461524912205)
- Personnel et embauche : [4](https://steamcommunity.com/app/868360/discussions/1/1752358461524416770),
  [5](https://steamcommunity.com/app/868360/discussions/0/3034851135411222906),
  [6](https://steamcommunity.com/app/868360/discussions/0/1643170903483124385)
- Suggestions, limites de taille, patients bloqués :
  [7](https://steamcommunity.com/app/868360/discussions/0/1736595227851627571),
  [8](https://steamcommunity.com/app/868360/discussions/0/2995417278821701093)
- Agents d'entretien, moral : [9](https://steamcommunity.com/app/868360/discussions/0/3412054783686507693)
- Mod de palette par modification de DLL (lu en entier) :
  [10](https://steamcommunity.com/app/868360/discussions/0/2995423424881194999)
- [VaporLens : synthèse des avis Steam](https://vaporlens.app/app/868360/project_hospital)
- [Quarter To Three : « Lupus Diagnosis Simulator »](https://forum.quartertothree.com/t/project-hospital-lupus-diagnosis-simulator/138349)
- [Community Resource Pack : commentaires](https://steamcommunity.com/sharedfiles/filedetails/comments/2015440728)
- [HZDev's QoL Mod (BepInEx)](https://catalogue.smods.ru/archives/378620)
- README du mod PooK Hospital Economy (copie locale du Workshop, item 3815904306)
