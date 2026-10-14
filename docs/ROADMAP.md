# Feuille de route – RPH Revamped Project Hospital

État au 14 octobre 2026. Les faits techniques viennent de la décompilation du jeu installé
(1.2.x, 4 DLC). Les problèmes de joueurs viennent des discussions Steam et d'agrégats d'avis.
Beaucoup de fils datent de 2018–2021 : certains bugs ont pu être corrigés. Ce que font les autres mods
vient de leurs pages Workshop, lues le 14 octobre 2026. Toutes les références sont dans « Sources », en
bas.

## 0. Vision : moderniser Project Hospital

Project Hospital simule bien la médecine (diagnostic, examens, traitements), mais sa gestion est
mince :
- l'argent rentre sans que les actes coûtent quoi que ce soit ;
- le personnel n'a ni marché de l'emploi, ni moral qui compte, ni relève réaliste ;
- le matériel ne s'use pas ;
- rien n'explique pourquoi l'hôpital perd de l'argent ou se bloque.

RPH modernise le jeu sur quatre piliers.

| Pilier | Objectif | Phases |
|---|---|---|
| Voir | Le joueur sait d'où vient chaque dollar et où l'hôpital bloque | 1, 2, 5 |
| Gérer | L'économie, le personnel et le matériel imposent de vrais choix | 3, 4, 6 |
| Faire vivre | Patients et personnel circulent et vivent l'hôpital de façon crédible | 5, 7, 8 |
| Progresser | Recherche, nouveaux domaines, contenu sur la durée | 9, 10 |

Principes communs à toutes les phases :
- un seul mod de code, chargé par le chargeur natif du jeu : pas de BepInEx, macOS et Windows ;
- textes en anglais et en français ;
- chaque fonction de gameplay a son interrupteur ; quand elle est coupée, le jeu se comporte comme
  l'original ;
- retirer le mod laisse les sauvegardes jouables ;
- tous les chiffres d'équilibrage sont dans des fichiers de données.

## 1. Ce que la décompilation change au plan

1. **Le jeu a son propre chargeur de mods de code.** Il charge les DLL d'un dossier
   `StreamingAssets/Addons/Mod…/` si ce dossier contient `0Harmony.dll` et si le joueur a activé
   « Enable loading of code mods » dans les options. Le point d'entrée est une classe qui hérite de
   `HospitalMod`. **BepInEx n'est donc pas nécessaire**, et c'est tant mieux : il est pénible à
   installer, surtout sur macOS.
2. **Harmony est obligatoire** : le chargeur exige `0Harmony.dll` dans le dossier. On s'en sert pour
   tous les patchs.
3. **Le jeu ne charge pas les DLL du Workshop.** L'abonnement télécharge bien les fichiers, DLL
   comprises, mais le jeu ne lit que le XML du dossier Workshop. Le joueur doit copier le mod dans
   `Addons/`. On distribue donc un zip (GitHub Releases) avec une procédure de copie, comme PooK et
   Bottleneck Monitor. Un petit chargeur peut lever cette contrainte (phase 11).
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
   Il utilise le chargeur natif comme nous, mais n'existe pas sur macOS. Il n'a pas de licence : on
   reprend les idées, pas le code (voir §3). Il patche les mêmes méthodes et ajoute son propre barème.
   Nos modules économie le détectent et coupent ce qui ferait double facturation (c'est déjà fait pour
   les examens).
8. **Le jeu calcule déjà la charge et l'attente.** Un moniteur de goulots peut lire ces valeurs au lieu
   de les recalculer : `Room.GetWorkloadLevel`, `Room.GetWorkloadDayPercent` et
   `GetWorkloadNightPercent`, l'attente des patients (`BehaviorPatient`, `m_continuousWaitingTime`),
   `BedWaitingStatus` et `ProcedureQueue`. Le jeu a aussi des événements (`GlobalEventManager` :
   accident, catastrophe naturelle, pandémie) et des objectifs (`Objective*`), utiles pour les phases 7
   et 8.

## 2. Problèmes récurrents

| Domaine | Problème | Origine | Réponse du mod | Phase |
|---|---|---|---|---|
| Interface | UI jugée « écrasante et peu intuitive », dure à gérer pour un grand hôpital, illisible sur Steam Deck | Avis Steam | Fenêtres de stats détaillées et lisibles | 2 |
| Interface | Impossible de savoir pourquoi on perd de l'argent : la radiologie et le labo paraissent toujours déficitaires | Avis, code | Grand livre par département, équipement et équipe | 1–2 |
| Interface | On ne voit pas ce qui bloque l'hôpital | Steam, Workshop | Moniteur de goulots avec la cause et l'action conseillée | 5 |
| Économie | Début de partie très serré, afflux de patients aléatoire d'un jour à l'autre | Steam | Lisser l'afflux, paiement journalier de l'hospitalisation | 3 |
| Économie | Une fois lancé, le jeu « tourne tout seul » | Steam (2024) | Coûts fixes qui croissent avec la taille, entretien, contrats | 3, 6 |
| Économie | Les examens et traitements ne coûtent rien à l'hôpital, l'équipement n'a aucun coût d'entretien | Code | Barème des actes, usure et réparations (faits en partie), gammes | 3, 6 |
| Économie | L'hospitalisation n'est payée qu'à la sortie : patients bloqués = aucune recette | Steam, code | Forfait journalier en plus du forfait de sortie (fait) | 3 |
| Économie | Prêt rudimentaire : paliers fixes de 20 000, plafond 500 000 | Code | Taux selon le prestige, échéancier | 3 |
| Économie | Les frais d'agence de recrutement sont comptés comme des « pénalités » | Code | Catégorie propre dans nos stats | 1 |
| RH | Le moral n'a aucune conséquence : le personnel ne démissionne jamais | Steam | Moral avec effets : fatigue, absences, démissions | 4 |
| RH | Niveau, compétences et salaire mal reliés, salaire figé à l'embauche | Steam, code | Négociation, augmentations, primes | 4 |
| RH | Relève des équipes à 7 h et 20 h : l'hôpital se bloque une à deux heures | Steam | Arrivée avant la relève, transmission au poste, couverture des retards | 4 |
| RH | Infirmiers et laborantins perdent leur temps à transporter patients et échantillons | Workshop | Brancardiers | 4 |
| RH | Agents d'entretien : un par département et un local chacun, pénible | Steam | Service de nettoyage par zones | 4 |
| RH | Alertes « personnel manquant » erronées, plafond de 200 employés | Steam (2018) | Le plafond est maintenant une option du jeu ; alertes de personnel dans nos stats | 4 |
| Matériel | Pas d'usure, de panne ni de gamme : l'achat est un choix unique | Code, Steam | Usure et pannes (faites), maintenance, gammes | 3, 6 |
| Matériel | Labos et radiologie saturés, patients en attente d'examens | Steam | Examens bloqués dans le moniteur, taux d'utilisation | 5, 6 |
| Flux patients | Patients bloqués (observation, hospitalisation), départs après un délai | Steam, Workshop | Moniteur de goulots, transfert de niveau de soins | 5 |
| Urgences | Les patients arrivent en ambulance sans aucun bilan | Workshop | Bilan et soins préhospitaliers | 7 |
| Patients | Les patients hospitalisés restent cloués au lit | Workshop | Vie des patients, cafétéria | 8 |
| Recherche | Aucun système de recherche ; demande d'un hôpital universitaire | Steam | Arbre de recherche, internes et étudiants | 9 |
| Domaines | Les mods de départements dupliquent symptômes et examens ; le CRP qui les fédère n'est plus maintenu | Workshop | Contenu propre préfixé `RPH_`, CRP optionnel | 10 |
| Performance | Ralentissements sur les grands hôpitaux, plantages | Avis Steam | Règle : aucun calcul par image, agrégats par heure ou par jour | Toutes |

## 3. Mods de référence : ce qu'on reprend

Règles :
1. On reprend des **idées**, jamais du code ni des assets. Aucun de ces mods n'a de licence libre : le
   dépôt GitHub de Loupito75 est « All rights reserved » et n'autorise que la consultation, PooK et
   Bottleneck Monitor n'ont pas de licence. On conçoit à partir de leur description publique et de notre
   décompilation du jeu, **sans lire leur code**.
2. Une fonction de gameplay reprise d'un mod a son interrupteur et **se coupe d'elle-même quand ce mod
   est chargé**. Le joueur qui préfère l'original le garde, sans conflit. Les fonctions qui ne font
   qu'observer (stats, moniteur) restent actives.
3. `CREDITS.md` cite chaque inspiration (« idée de … »).
4. Ce que RPH apporte en plus : macOS sans BepInEx, un seul mod à installer, chaque montant dans le grand
   livre, l'historique dans la fenêtre de stats.

| Mod (auteur) | Technique | Idées reprises | Ce qu'on ajoute | Phase |
|---|---|---|---|---|
| PooK Hospital Economy (N0d PooK) | Chargeur natif, Windows seulement | Hospitalisation payée à l'heure chaque soir ; coût des examens ; coût journalier des lits selon le lieu des soins (service, USC, USI, trauma) ; recettes des arrivées en ambulance et en hélicoptère ; liste de candidats gratuite et atouts cachés payants ; projets : nouveaux assureurs, amendes | macOS ; prix dans les infobulles (demandé par ses joueurs) | 3, 4 |
| Bottleneck Monitor (LiPadu) | Chargeur natif, lecture seule | Panneau des goulots : files, examens bloqués, lits, personnel ; alertes critiques ou à surveiller ; clic qui centre la caméra ; action conseillée ; seuils réglables ; mise à jour depuis le dossier Workshop | Historique par jour et tendances dans la fenêtre de stats | 4, 5, 11 |
| Hospital Shift Handover (Loupito75) | BepInEx | Arrivée avant l'équipe, transmission au même poste, l'équipe sortante couvre un retard, vestiaires | Temps de chevauchement payé et visible dans le grand livre | 4 |
| Hospital Porters (Loupito75) | BepInEx | Rôle de brancardier, local dédié, transport des patients et des échantillons, tournées, repli sur infirmiers et laborantins | Coût et productivité dans l'onglet Personnel | 4 |
| Hospital Traffic Control (Loupito75) | BepInEx | Portes à sens unique, règles d'accès, pas de raccourci par les salles, agents d'entretien plus discrets | — | 4, 5 |
| Hospital Care Level Transfer (Loupito75) | BepInEx | Passage d'USC en service conventionnel, automatique ou manuel, avec garde-fous ; infobulle qui explique un refus | Débloquer aussi l'observation des urgences | 5 |
| Hospital EMS (Loupito75) | BepInEx | Bilan et soins dans l'ambulance ou l'hélicoptère ; correctifs du brancard et de la réservation de lit | Types d'ambulances, coût des soins en route | 7 |
| Hospital Patient Life (Loupito75) | BepInEx | Patients hospitalisés qui sortent de leur chambre, cafétéria, coucher et lever échelonnés | Recettes de cafétéria, satisfaction qui pèse sur le prestige | 8 |
| Hospital Always Lit (Loupito75) | BepInEx | Couloirs et salles d'attente toujours éclairés | — | 8 |
| Hospital Mod Update Checker (Loupito75) | BepInEx, réseau | Avis de mise à jour sur l'écran titre | Sans réseau : comparaison avec la copie Workshop | 11 |
| Hospital Radio (Loupito75) | BepInEx | Hors périmètre : aucun effet de jeu, environ 120 Mo d'audio | — | — |

## 4. Dépendances

| Dépendance | Décision | Raison |
|---|---|---|
| Harmony (MIT) | **Oui**, livré en `0Harmony.dll` (build net35 de Lib.Harmony 2.2.2) | Exigé par le chargeur du jeu. 2.3+ fait planter le jeu sur macOS. |
| BepInEx | **Non** | Inutile avec le chargeur natif, difficile sur macOS. On reste compatible avec les mods qui l'utilisent. |
| Community Resource Pack | **Plus tard, optionnel** (phase 10) | Utile seulement pour du contenu médical partagé. Plus maintenu depuis environ 2022 : risque. |
| PooK Hospital Economy | **Incompatible** avec nos modules économie | Mêmes patchs, double facturation. Détection, avertissement, et nos fonctions en double se coupent. |
| Mods de Loupito75 | **Compatibles, jamais requis** | Notre équivalent se coupe quand le sien est chargé. |
| Bottleneck Monitor | **Compatible** | Lecture seule des deux côtés : aucun conflit. |
| HZDev's QoL Mod | **Compatible, à tester** | Change l'IA de diagnostic, pas nos systèmes. |

## 5. Horizons

| Version | Contenu | Phases |
|---|---|---|
| alpha 0.x | Statistiques avancées, puis économie | 0–3 |
| 1.0 | Personnel, flux et goulots | 4–5 |
| 2.0 | Matériel, urgences, vie des patients | 6–8 |
| 3.0 | Recherche, domaines | 9–10 |

La phase 11 (publication et maintenance) tourne en continu dès l'alpha. Chaque phase se termine par une
version jouable et peut sortir sans les suivantes. Les phases 1 et 2 ne changent pas le gameplay : elles
servent à mesurer, pour équilibrer tout le reste avec des chiffres.

## 6. Plan étape par étape

**État au 14 octobre 2026** : phases 0, 1 et 2 codées, compilées et déployées sur macOS ; 45 tests
unitaires du cœur passent. **Reste à vérifier en jeu** (critères « Terminé quand » ci-dessous), puis
sous Windows. Phase 3 commencée : barème des examens. L'usure des machines, prévue avec le matériel, est
faite en avance. Écarts par rapport au plan initial, pour rester simple :
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

### Phase 3 – Économie (en cours)

Fait :
- Barème des examens, payé par l'assurance au département qui fait l'examen
  (`mod/Database/Economy/ExamPrices.xml`), coupé quand PooK est chargé.
- Usure, pannes aléatoires et réparations payantes des machines
  (`mod/Database/Economy/MachineMaintenance.xml`).
- Journées d'hospitalisation : chaque nuit, chaque patient qui a un lit rapporte un forfait journalier et
  coûte des soins, selon son type d'hospitalisation (`mod/Database/Economy/InpatientDays.xml`). Le
  forfait du diagnostic payé à la sortie reste.
- Forfait de transport pour les patients amenés par notre ambulance ou notre hélicoptère ; les
  ambulances de la ville ne rapportent rien (`mod/Database/Economy/AmbulanceFees.xml`).

Pas de coût par traitement : il est compris dans le coût journalier des soins, pour rester simple.

Reste :
1. Prix affichés dans les infobulles des examens.
2. Prêts : taux selon le prestige, échéancier, découvert pénalisé.
3. Assurances : contrats négociés, plafonds de volume, nouveaux assureurs avec leurs objectifs et leurs
   types de patients.
4. Amendes : mauvais diagnostic, décès évitable, transfert vers un autre hôpital.
5. Courbe de difficulté : lisser le début de partie, coûts fixes qui croissent avec la taille.
6. Équilibrage avec la fenêtre de stats : marges cibles par taille d'hôpital, préréglages Facile,
   Normal, Difficile.

**Terminé quand** un petit, un moyen et un grand hôpital tombent dans les marges cibles de chaque
préréglage.

### Phase 4 – Personnel (priorité après l'économie)

1. **Mesurer d'abord** : onglet Personnel (coût et productivité par employé, heures, départs) et
   alertes de personnel (département sous le minimum du jeu, employé épuisé, employé sans poste).
2. **Relève des équipes** : le personnel arrive avant le début de son équipe, la transmission se fait
   au poste, l'équipe sortante couvre un retard de 15 à 30 minutes. Le chevauchement est payé et
   visible dans le grand livre.
3. **Brancardiers** : nouveau rôle et nouveau local ; transport des patients (brancard, fauteuil) et
   des échantillons, par tournées ; infirmiers et laborantins prennent le relais quand il n'y en a pas.
   Vérifier d'abord si les sprites du jeu suffisent (tenue recolorée) avant de dessiner un personnage.
4. **Agence et marché de l'emploi** : liste de candidats gratuite, atouts cachés révélés contre
   paiement ; vivier selon le prestige et le salaire proposé, délai de recrutement, postes ouverts.
5. **Salaires** : négociation à l'embauche, augmentations, primes.
6. **Moral avec conséquences** : fatigue, absences, démissions.
7. **Formation**, en lien avec la formation du DLC Hospital Services.
8. **Entretien** : service de nettoyage par zones ; les agents évitent les salles en pleine procédure et
   attendent devant une salle occupée.

**Terminé quand** l'hôpital ne se bloque plus à 7 h ni à 20 h, et que l'onglet Personnel explique
chaque démission.

### Phase 5 – Flux et goulots

1. **Moniteur de goulots**, en lecture seule, dans la fenêtre de stats et derrière une icône du HUD qui
   affiche le nombre de problèmes :
   - files : patients près de partir, salles d'attente pleines, attente moyenne trop longue, départs et
     décès du jour ;
   - examens et traitements bloqués depuis plus d'une heure, groupés par département, procédure et
     cause ;
   - lits : aucun lit libre du bon type, service presque plein, salle que le jeu ne peut pas utiliser
     (avec la raison) ;
   - personnel : les alertes de la phase 4.

   Chaque alerte est « critique » ou « à surveiller » et propose une action. Un clic centre la caméra,
   un second passe au cas suivant. Les seuils sont dans les données. Les compteurs sont gardés par jour
   pour montrer des tendances.
2. **Transfert de niveau de soins** : un patient stable d'USC passe en service conventionnel du même
   département, automatiquement ou à la main, avec des garde-fous (danger, isolement, traitement en
   cours). Une infobulle explique un refus. Débloquer aussi les patients d'observation des urgences qui
   attendent un lit.
3. **Circulation** : portes à sens unique, salles fonctionnelles évitées comme raccourcis, règles
   d'accès plus strictes.

**Terminé quand** chaque patient parti sans soins apparaît dans le moniteur avec sa cause.

### Phase 6 – Matériel

1. Maintenance par des techniciens ou par contrat (l'usure et les pannes sont faites en phase 3).
2. Gammes d'équipement (basique, standard, haut de gamme) : vitesse, précision du diagnostic, confort.
3. Nouveaux objets avec nos propres graphismes (atlas isométriques 4 directions, au format des
   tutoriels officiels), listés dans `CREDITS.md`.
4. Onglet Matériel : taux d'utilisation, rentabilité par machine, pannes.

### Phase 7 – Urgences et préhospitalier

1. Bilan à bord de l'ambulance ou de l'hélicoptère, noté dans le dossier du patient ; soins en route
   quand ils sont indiqués (pansement compressif, oxygène, perfusion, antalgiques, transfusion).
2. Types d'ambulances : de base, médicalisée, hélicoptère, avec des bilans et des soins différents.
3. Coût des soins en route dans le grand livre, remboursement selon l'assureur.
4. Correctifs du jeu de base signalés par la communauté : brancard perdu au chargement d'une
   sauvegarde, lit libéré trop tôt pendant un transfert. Les reproduire nous-mêmes avant de corriger.

### Phase 8 – Vie des patients et confort

1. Les patients hospitalisés qui peuvent marcher sortent de leur chambre : salons, bancs dehors, autres
   étages.
2. Cafétéria : repas gratuits pour les patients hospitalisés, payants pour ceux de la consultation
   (recette dans le grand livre).
3. Coucher et lever échelonnés.
4. Satisfaction des patients qui pèse sur le prestige et les assureurs ; objets de salle d'attente avec
   un vrai effet.
5. Sécurité incendie utile et obligatoire, avec des événements.
6. En option : couloirs et salles d'attente toujours éclairés.

### Phase 9 – Recherche

1. Points de recherche gagnés sur les cas complexes, les autopsies (DLC) et l'enseignement.
2. Arbre de recherche qui débloque des gammes d'équipement, des procédures et des gains d'efficacité.
   Vérifier si le panneau d'arbre de dépendances du jeu (`DependencyTreePanelController`) peut servir
   de base.
3. Hôpital universitaire : internes et étudiants.
4. Onglet stats Recherche.

### Phase 10 – Domaines (départements et contenu médical)

1. Décider entre dépendre du CRP ou créer notre propre contenu avec des identifiants `RPH_`.
2. Nouveaux départements et diagnostics en XML, avec des `ProcedureScript` en C# si besoin
   (le jeu les retrouve dans les types du mod).
3. Orientations entre départements (les patients bloqués sont traités en phase 5).

### Phase 11 – Publication et maintenance (en continu dès l'alpha)

1. Distribution : zip sur GitHub Releases, page Workshop avec la procédure de copie.
2. **Mises à jour par Steam** : un petit chargeur, copié une seule fois dans `Addons/`, charge la DLL
   la plus récente de notre item Workshop (idée de Bottleneck Monitor). Il ne charge que notre propre
   identifiant d'item.
3. Avis de mise à jour sur l'écran titre, sans réseau : comparer la version chargée avec celle du
   dossier Workshop.
4. Raccourci de la fenêtre configurable : **F9 est déjà pris par Hospital Radio** (Bottleneck Monitor
   utilise F11).
5. Matrice de compatibilité : avec et sans les 4 DLC, PooK, Bottleneck Monitor, les mods de Loupito75,
   HZDev's QoL Mod.
6. Numéros de version, journal des modifications, migration du fichier annexe entre versions.
7. Test sur macOS et Windows à chaque version.

## 7. Risques et questions ouvertes

- **Harmony sur macOS** : fonctionne avec Lib.Harmony 2.2.2 ; 2.3 et plus fait planter le Mono du jeu.
  Ne pas mettre à jour.
- **Distribution manuelle** : chaque joueur doit copier le dossier et activer l'option. Réponse : le
  chargeur Workshop de la phase 11, et un message clair au démarrage.
- **Doublons avec des mods actifs** : Loupito75 publie des versions chaque semaine. Si sa fonction et
  la nôtre tournent ensemble, elles patchent les mêmes méthodes. Détection par nom d'assembly
  (`Loupito75.*`), à vérifier en jeu, puis coupure de notre équivalent.
- **Coût graphique** : brancardiers, chariots et nouveaux objets demandent des sprites isométriques en
  4 directions.
- **Ampleur** : onze phases pour un projet personnel. Chaque phase doit pouvoir sortir seule.
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

Mods de référence (pages Workshop lues le 14 octobre 2026) :

- [PooK Hospital Economy](https://steamcommunity.com/sharedfiles/filedetails/?id=3815904306)
- [Bottleneck Monitor](https://steamcommunity.com/sharedfiles/filedetails/?id=3817130089)
- Loupito75 : [Shift Handover](https://steamcommunity.com/sharedfiles/filedetails/?id=3799192971),
  [Porters](https://steamcommunity.com/sharedfiles/filedetails/?id=3810586729),
  [Traffic Control](https://steamcommunity.com/sharedfiles/filedetails/?id=3797445876),
  [Care Level Transfer](https://steamcommunity.com/sharedfiles/filedetails/?id=3798356542),
  [EMS](https://steamcommunity.com/sharedfiles/filedetails/?id=3799207833),
  [Patient Life](https://steamcommunity.com/sharedfiles/filedetails/?id=3803626360),
  [Always Lit](https://steamcommunity.com/sharedfiles/filedetails/?id=3797438326),
  [Mod Update Checker](https://steamcommunity.com/sharedfiles/filedetails/?id=3806092398),
  [Radio](https://steamcommunity.com/sharedfiles/filedetails/?id=3809022219),
  [dépôt GitHub (All rights reserved)](https://github.com/Loupito75/Project-Hospital-Mods)
- [HZDev's QoL Mod (Workshop)](https://steamcommunity.com/sharedfiles/filedetails/?id=3550712901)
